using System;
using Portals.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace Portals.Traversal
{
    /// <summary>
    /// Continuous split-object portal transition.
    ///
    /// A single Rigidbody cannot occupy two disconnected regions, so the original body
    /// remains authoritative. A proxy clone is created on the destination side, follows
    /// the portal-transformed pose every frame, and has all colliders disabled so it
    /// never simulates independently. Geometry on both sides is clipped against the
    /// portal planes using the _ClipPlane/_ClipSide material properties.
    ///
    /// Limitation: collision response on the destination side is approximated by the
    /// original body's collision in the source space; true two-sided collision is not
    /// possible without a full dual-body simulation, which is out of scope for Unity's
    /// single-body Rigidbody model.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalSplitTransition : MonoBehaviour
    {
        public enum State { Idle, Transitioning, Completed }

        [Header("Transition")]
        [SerializeField, Min(0f)] private float m_CompletionThreshold = 0.98f;
        [SerializeField, Min(0f)] private float m_ReactivationCooldown = 0.25f;

        public State CurrentState { get; private set; } = State.Idle;
        public PortalEndpoint Entrance { get; private set; }
        public PortalEndpoint Exit { get; private set; }
        public float Progress { get; private set; }

        public event Action<PortalSplitTransition> Completed;

        private GameObject m_Proxy;
        private Rigidbody m_SourceBody;
        private Rigidbody m_ProxyBody;
        private Renderer[] m_SourceRenderers;
        private Renderer[] m_ProxyRenderers;
        private float m_LastTransitionTime = -10f;
        private bool m_Initialized;

        private void Awake()
        {
            m_SourceBody = GetComponent<Rigidbody>();
            CacheRenderers();
            m_Initialized = true;
        }

        private void OnDisable()
        {
            Abort();
        }

        private void OnDestroy()
        {
            Abort();
        }

        private void CacheRenderers()
        {
            m_SourceRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public bool Begin(PortalEndpoint entrance, PortalEndpoint exit)
        {
            if (entrance == null || exit == null) return false;
            if (CurrentState == State.Transitioning) return false;
            if (Time.time - m_LastTransitionTime < m_ReactivationCooldown) return false;

            Entrance = entrance;
            Exit = exit;
            Progress = 0f;
            CurrentState = State.Transitioning;
            m_LastTransitionTime = Time.time;

            CreateProxy();
            return true;
        }

        public void Tick()
        {
            if (CurrentState != State.Transitioning) return;
            if (Entrance == null || Exit == null || m_Proxy == null)
            {
                Abort();
                return;
            }

            Profiler.BeginSample("PortalSplitTransition.Tick");
            try
            {
                UpdateProgress();
                SyncProxy();
                ApplyClipping();
                if (Progress >= m_CompletionThreshold) Complete();
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public void Abort()
        {
            if (m_Proxy != null)
            {
                Destroy(m_Proxy);
                m_Proxy = null;
            }
            m_ProxyBody = null;
            m_ProxyRenderers = null;
            CurrentState = State.Idle;
            Entrance = null;
            Exit = null;
            Progress = 0f;
        }

        private void CreateProxy()
        {
            if (m_Proxy != null) Destroy(m_Proxy);

            m_Proxy = Instantiate(gameObject);
            m_Proxy.name = gameObject.name + " (Portal Proxy)";
            m_Proxy.transform.SetPositionAndRotation(transform.position, transform.rotation);

            // Remove behaviours that must not duplicate.
            foreach (var t in m_Proxy.GetComponentsInChildren<PortalTraverser>(true)) Destroy(t);
            foreach (var s in m_Proxy.GetComponentsInChildren<PortalSplitTransition>(true)) Destroy(s);

            // Disable all colliders on proxy.
            foreach (var col in m_Proxy.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            m_ProxyBody = m_Proxy.GetComponent<Rigidbody>();
            if (m_ProxyBody != null)
            {
                m_ProxyBody.isKinematic = true;
                m_ProxyBody.useGravity = false;
                m_ProxyBody.linearVelocity = Vector3.zero;
                m_ProxyBody.angularVelocity = Vector3.zero;
            }

            // Instantiate materials so clipping does not affect the source.
            m_ProxyRenderers = m_Proxy.GetComponentsInChildren<Renderer>(true);
            foreach (var r in m_ProxyRenderers)
            {
                Material[] mats = r.materials; // instance
                r.materials = mats;
            }
        }

        private void UpdateProgress()
        {
            if (Entrance == null) return;

            // Use collider bounds extent along portal normal for a stable progress.
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool measured = false;

            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                Vector3 local = Entrance.WorldToLocalPoint(transform.position);
                Progress = Mathf.Clamp01((-local.z + 0.5f) / 1.0f);
                return;
            }

            foreach (var col in colliders)
            {
                if (col == null || !col.enabled) continue;
                Bounds b = col.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 pt = new Vector3(
                        (corner & 1) != 0 ? c.x + e.x : c.x - e.x,
                        (corner & 2) != 0 ? c.y + e.y : c.y - e.y,
                        (corner & 4) != 0 ? c.z + e.z : c.z - e.z);
                    Vector3 l = Entrance.WorldToLocalPoint(pt);
                    min = Vector3.Min(min, l);
                    max = Vector3.Max(max, l);
                    measured = true;
                }
            }

            if (!measured)
            {
                Vector3 local = Entrance.WorldToLocalPoint(transform.position);
                Progress = Mathf.Clamp01((-local.z + 0.5f) / 1.0f);
                return;
            }

            // Progress: 0 when whole object in front (maxZ > 0, minZ > 0), 1 when whole behind (maxZ < 0).
            float totalDepth = (max.z - min.z);
            if (totalDepth < 1e-4f) totalDepth = 1f;
            // amount of object that has passed plane = clamp((0 - min.z) / totalDepth, 0, 1)
            float passed = (0f - min.z) / totalDepth;
            Progress = Mathf.Clamp01(passed);
        }

        private void SyncProxy()
        {
            if (m_Proxy == null || Exit == null || Entrance == null) return;
            Matrix4x4 entrance = Entrance.transform.localToWorldMatrix;
            Matrix4x4 exit = Exit.transform.localToWorldMatrix;
            Matrix4x4 flip = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
            Matrix4x4 portalMatrix = exit * flip * entrance.inverse;

            m_Proxy.transform.SetPositionAndRotation(
                portalMatrix.MultiplyPoint3x4(transform.position),
                portalMatrix.rotation * transform.rotation);

            if (m_SourceBody != null && m_ProxyBody != null)
            {
                m_ProxyBody.linearVelocity = portalMatrix.rotation * m_SourceBody.linearVelocity;
                m_ProxyBody.angularVelocity = portalMatrix.rotation * m_SourceBody.angularVelocity;
            }
        }

        private void ApplyClipping()
        {
            if (Entrance == null || Exit == null) return;

            Vector3 inNormal = Entrance.Forward;
            Vector3 inPoint = Entrance.Position;
            Vector4 inPlane = new Vector4(inNormal.x, inNormal.y, inNormal.z, -Vector3.Dot(inNormal, inPoint));

            Vector3 outNormal = Exit.Forward;
            Vector3 outPoint = Exit.Position;
            Vector4 outPlane = new Vector4(outNormal.x, outNormal.y, outNormal.z, -Vector3.Dot(outNormal, outPoint));

            if (m_SourceRenderers == null) m_SourceRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in m_SourceRenderers)
            {
                if (r == null) continue;
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_ClipPlane"))
                    {
                        mat.SetVector("_ClipPlane", inPlane);
                        mat.SetFloat("_ClipSide", -1f); // keep source side (in front)
                    }
                }
            }

            if (m_ProxyRenderers == null && m_Proxy != null)
                m_ProxyRenderers = m_Proxy.GetComponentsInChildren<Renderer>(true);
            if (m_ProxyRenderers != null)
            {
                foreach (var r in m_ProxyRenderers)
                {
                    if (r == null) continue;
                    foreach (var mat in r.materials)
                    {
                        if (mat == null) continue;
                        if (mat.HasProperty("_ClipPlane"))
                        {
                            mat.SetVector("_ClipPlane", outPlane);
                            mat.SetFloat("_ClipSide", 1f); // keep destination side
                        }
                    }
                }
            }
        }

        private void Complete()
        {
            CurrentState = State.Completed;
            Completed?.Invoke(this);
            m_LastTransitionTime = Time.time;
            Abort();
        }
    }
}
