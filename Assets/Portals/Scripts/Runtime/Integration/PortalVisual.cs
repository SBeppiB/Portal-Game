using System;
using System.Collections;
using Portals.Core;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>Default solid colours applied at setup. Each PortalVisual's colours remain Inspector-configurable.</summary>
    public static class PortalVisualColours
    {
        public static readonly Color LocaversalSurface = new Color(0.08f, 0.5f, 1f, 1f);
        public static readonly Color LocaversalFrame = new Color(0.55f, 0.88f, 1f, 1f);
        public static readonly Color InterversalSurface = new Color(0.85f, 0.15f, 0.7f, 1f);
        public static readonly Color InterversalFrame = new Color(1f, 0.6f, 0.92f, 1f);

        public static void Apply(PortalVisual visual, PortalKind kind, Vector2 halfSize)
        {
            if (visual == null) return;
            if (kind == PortalKind.Interversal)
                visual.Configure(InterversalSurface, InterversalFrame, halfSize);
            else
                visual.Configure(LocaversalSurface, LocaversalFrame, halfSize);
        }
    }

    /// <summary>
    /// Simple opaque portal rendering: a solid-colour elliptical disc plus a thin frame.
    /// No transparency, no portal-window rendering and no custom shaders. Children are
    /// parented to the endpoint's Opening transform so PortalSurfaceAnchor movement is
    /// followed automatically. No colliders are created, so placement/traversal raycasts
    /// are unaffected.
    ///
    /// The surface and frame are animated together by scaling both children. The logical
    /// aperture (PortalEndpoint.OpeningHalfSize) is never changed, so portal mathematics
    /// stays untouched. An opening animation blocks traversal until it completes; a
    /// closing animation hides the visuals at its end and lets the lifecycle manager pool
    /// the endpoint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalVisual : MonoBehaviour
    {
        public enum AnimationState
        {
            None,
            Opening,
            Closing
        }

        [Header("Colours (Inspector configurable)")]
        [SerializeField] private Color m_SurfaceColor = new Color(0.1f, 0.55f, 1f, 1f);
        [SerializeField] private Color m_FrameColor = new Color(0.55f, 0.9f, 1f, 1f);
        [SerializeField, Min(0f)] private float m_EmissiveStrength = 0.6f;

        [Header("Geometry")]
        [SerializeField, Range(8, 64)] private int m_Segments = 32;
        [SerializeField, Min(0f)] private float m_FrameThickness = 0.12f;
        [SerializeField] private float m_SurfaceForwardOffset = 0.002f;
        [SerializeField] private float m_FrameForwardOffset = 0.001f;

        [Header("Swirl (procedural)")]
        [SerializeField, Min(0f)] private float m_SwirlSpeed = 1.2f;
        [SerializeField, Min(0f)] private float m_SwirlIntensity = 1.0f;
        [SerializeField, Min(0f)] private float m_NoiseScale = 4.0f;
        [SerializeField] private Color m_InnerColor = new Color(0.05f, 0.2f, 0.6f, 1f);
        [SerializeField] private Color m_OuterColor = new Color(0.4f, 0.85f, 1f, 1f);
        [SerializeField] private Color m_RimColor = new Color(0.7f, 0.95f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float m_CenterAlpha = 0.18f;
        [SerializeField, Range(0f, 1f)] private float m_MiddleAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float m_OuterAlpha = 0.95f;

        [Header("Open / Close Animation")]
        [SerializeField, Min(0f)] private float m_OpenDuration = 0.4f;
        [SerializeField, Min(0f)] private float m_CloseDuration = 0.4f;

        private PortalEndpoint m_Portal;
        private GameObject m_SurfaceObject;
        private GameObject m_FrameObject;
        private Mesh m_SurfaceMesh;
        private Mesh m_FrameMesh;
        private Material m_SurfaceMaterial;
        private Material m_FrameMaterial;
        private Vector2 m_BuiltHalfSize = new Vector2(-1f, -1f);
        private int m_BuiltSegments = -1;
        private bool m_Subscribed;

        private Coroutine m_Animation;
        private int m_AnimationGeneration;
        private bool m_AnimationPlaying;
        private AnimationState m_AnimationState = AnimationState.None;
        private float m_AnimationProgress;

        public Color SurfaceColor => m_SurfaceColor;
        public Color FrameColor => m_FrameColor;
        public GameObject SurfaceObject => m_SurfaceObject;
        public GameObject FrameObject => m_FrameObject;

        public float OpenDuration => m_OpenDuration;
        public float CloseDuration => m_CloseDuration;
        public bool IsAnimating => m_Animation != null;
        public bool IsOpening => m_AnimationState == AnimationState.Opening;
        public bool IsClosing => m_AnimationState == AnimationState.Closing;
        public AnimationState CurrentAnimation => m_AnimationState;
        public float AnimationProgress => m_AnimationProgress;

        private void Awake()
        {
            if (m_Portal == null) m_Portal = GetComponent<PortalEndpoint>();
            Rebuild();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyActiveState();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopRunningAnimation();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            StopRunningAnimation();

            if (m_SurfaceObject != null) Destroy(m_SurfaceObject);
            if (m_FrameObject != null) Destroy(m_FrameObject);
            if (m_SurfaceMesh != null) Destroy(m_SurfaceMesh);
            if (m_FrameMesh != null) Destroy(m_FrameMesh);
            if (m_SurfaceMaterial != null) Destroy(m_SurfaceMaterial);
            if (m_FrameMaterial != null) Destroy(m_FrameMaterial);
        }

        /// <summary>Configures colours and rebuilds the disc/frame for the given aperture.</summary>
        public void Configure(Color surfaceColor, Color frameColor, Vector2 halfSize)
        {
            m_SurfaceColor = surfaceColor;
            m_FrameColor = frameColor;
            if (m_Portal == null) m_Portal = GetComponent<PortalEndpoint>();
            if (m_Portal != null) m_Portal.OpeningHalfSize = halfSize;

            // A reused endpoint must not inherit a stale animation or a collapsed scale.
            StopRunningAnimation();
            Rebuild();
            SetChildrenScale(Vector3.one);
            ApplyMaterials();
            ApplyActiveState();
        }

        /// <summary>Sets both animation durations. Zero is allowed for instant transitions.</summary>
        public void SetAnimationDurations(float openDuration, float closeDuration)
        {
            m_OpenDuration = Mathf.Max(0f, openDuration);
            m_CloseDuration = Mathf.Max(0f, closeDuration);
        }

        /// <summary>
        /// Starts the opening animation: the surface/frame begin collapsed to zero and grow
        /// to the configured aperture. Traversal is disabled until the animation completes.
        /// Any previous animation on this visual is cancelled first.
        /// </summary>
        public void PlayOpen(Action onComplete = null)
        {
            StopRunningAnimation();
            m_AnimationPlaying = true;
            m_AnimationState = AnimationState.Opening;
            m_AnimationProgress = 0f;

            if (m_Portal != null) m_Portal.SetTraversalEnabled(false);
            SetChildrenScale(Vector3.zero);
            ApplyActiveState();

            m_Animation = StartCoroutine(AnimateScale(
                Vector3.zero, Vector3.one, m_OpenDuration,
                () =>
                {
                    if (m_Portal != null) m_Portal.SetTraversalEnabled(true);
                    FinishAnimation(onComplete);
                }));
        }

        /// <summary>
        /// Starts the closing animation. Traversal is blocked immediately (the endpoint is
        /// deactivated) while the surface/frame shrink to zero. The callback runs once the
        /// shrink completes, which is where goo spawning and pooling happen.
        /// </summary>
        public void PlayClose(Action onComplete = null)
        {
            StopRunningAnimation();
            m_AnimationPlaying = true;
            m_AnimationState = AnimationState.Closing;
            m_AnimationProgress = 0f;

            // Block transport and entry in the same frame the close is requested.
            if (m_Portal != null) m_Portal.SetActive(false);

            Vector3 start = m_SurfaceObject != null
                ? m_SurfaceObject.transform.localScale
                : Vector3.one;
            if (start.sqrMagnitude < 1e-8f) start = Vector3.one;

            m_Animation = StartCoroutine(AnimateScale(
                start, Vector3.zero, m_CloseDuration,
                () => FinishAnimation(onComplete)));
        }

        private void FinishAnimation(Action onComplete)
        {
            m_Animation = null;
            m_AnimationPlaying = false;
            m_AnimationState = AnimationState.None;
            ApplyActiveState();
            onComplete?.Invoke();
        }

        private void StopRunningAnimation()
        {
            if (m_Animation != null)
            {
                StopCoroutine(m_Animation);
                m_Animation = null;
            }
            // Invalidate any callback that somehow survived the explicit stop.
            m_AnimationGeneration++;
            m_AnimationPlaying = false;
            m_AnimationState = AnimationState.None;
            m_AnimationProgress = 0f;
        }

        private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration, Action onComplete)
        {
            int generation = m_AnimationGeneration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return null;
                if (generation != m_AnimationGeneration) yield break;

                elapsed += Time.deltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                m_AnimationProgress = t;

                // Smoothstep keeps the expansion/contraction gentle while still finishing
                // exactly at the configured duration.
                float smooth = t * t * (3f - 2f * t);
                SetChildrenScale(Vector3.Lerp(from, to, smooth));
            }

            if (generation != m_AnimationGeneration) yield break;

            SetChildrenScale(to);
            m_AnimationProgress = 1f;
            onComplete?.Invoke();
        }

        private void SetChildrenScale(Vector3 scale)
        {
            if (m_SurfaceObject != null) m_SurfaceObject.transform.localScale = scale;
            if (m_FrameObject != null) m_FrameObject.transform.localScale = scale;
        }

        private void Update()
        {
            if (m_Portal == null) return;

            // Keep the mesh in sync if the aperture is changed in the Inspector.
            Vector2 half = m_Portal.OpeningHalfSize;
            if (Mathf.Abs(half.x - m_BuiltHalfSize.x) > 0.001f ||
                Mathf.Abs(half.y - m_BuiltHalfSize.y) > 0.001f ||
                m_Segments != m_BuiltSegments)
            {
                Rebuild();
            }
        }

        private void Subscribe()
        {
            if (m_Subscribed || m_Portal == null) return;
            m_Portal.ActiveChanged += OnPortalActiveChanged;
            m_Subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_Subscribed || m_Portal == null) return;
            m_Portal.ActiveChanged -= OnPortalActiveChanged;
            m_Subscribed = false;
        }

        private void OnPortalActiveChanged(PortalEndpoint endpoint)
        {
            ApplyActiveState();
        }

        private void ApplyActiveState()
        {
            // During an animation the geometry must stay visible even though the endpoint
            // is deactivated for closing (or awaiting a full opening).
            bool visible = m_AnimationPlaying || m_Portal == null || m_Portal.IsActive;
            if (m_SurfaceObject != null) m_SurfaceObject.SetActive(visible);
            if (m_FrameObject != null) m_FrameObject.SetActive(visible);
        }

        private void Rebuild()
        {
            Vector2 halfSize = m_Portal != null ? m_Portal.OpeningHalfSize : new Vector2(1f, 1.5f);
            if (m_SurfaceMesh != null) Destroy(m_SurfaceMesh);
            if (m_FrameMesh != null) Destroy(m_FrameMesh);

            m_SurfaceMesh = BuildDisc(halfSize, m_Segments, m_SurfaceForwardOffset);
            m_FrameMesh = BuildFrame(halfSize, m_Segments, m_FrameThickness, m_FrameForwardOffset);
            m_BuiltHalfSize = halfSize;
            m_BuiltSegments = m_Segments;

            if (m_SurfaceObject == null)
                m_SurfaceObject = CreateChild("Portal Surface", m_SurfaceMesh);
            else
                m_SurfaceObject.GetComponent<MeshFilter>().sharedMesh = m_SurfaceMesh;

            if (m_FrameObject == null)
                m_FrameObject = CreateChild("Portal Frame", m_FrameMesh);
            else
                m_FrameObject.GetComponent<MeshFilter>().sharedMesh = m_FrameMesh;

            ApplyMaterials();
            ApplyActiveState();
        }

        private GameObject CreateChild(string name, Mesh mesh)
        {
            GameObject child = new GameObject(name);
            Transform parent = m_Portal != null ? m_Portal.Opening : transform;
            child.transform.SetParent(parent, false);
            child.layer = 2; // Ignore Raycast - visuals must never affect placement queries.

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return child;
        }

        private void ApplyMaterials()
        {
            if (m_SurfaceObject == null || m_FrameObject == null) return;

            if (m_SurfaceMaterial == null) m_SurfaceMaterial = CreateMaterial();
            if (m_FrameMaterial == null) m_FrameMaterial = CreateMaterial();

            SetMaterialColour(m_SurfaceMaterial, m_SurfaceColor, m_EmissiveStrength);
            SetMaterialColour(m_FrameMaterial, m_FrameColor, m_EmissiveStrength * 0.5f);

            ApplySwirlParams(m_SurfaceMaterial);

            m_SurfaceObject.GetComponent<MeshRenderer>().sharedMaterial = m_SurfaceMaterial;
            m_FrameObject.GetComponent<MeshRenderer>().sharedMaterial = m_FrameMaterial;
        }

        private void ApplySwirlParams(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_SwirlSpeed")) mat.SetFloat("_SwirlSpeed", m_SwirlSpeed);
            if (mat.HasProperty("_SwirlIntensity")) mat.SetFloat("_SwirlIntensity", m_SwirlIntensity);
            if (mat.HasProperty("_NoiseScale")) mat.SetFloat("_NoiseScale", m_NoiseScale);
            if (mat.HasProperty("_InnerColor")) mat.SetColor("_InnerColor", m_InnerColor);
            if (mat.HasProperty("_OuterColor")) mat.SetColor("_OuterColor", m_OuterColor);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", m_RimColor);
            if (mat.HasProperty("_CenterAlpha")) mat.SetFloat("_CenterAlpha", m_CenterAlpha);
            if (mat.HasProperty("_MiddleAlpha")) mat.SetFloat("_MiddleAlpha", m_MiddleAlpha);
            if (mat.HasProperty("_OuterAlpha")) mat.SetFloat("_OuterAlpha", m_OuterAlpha);
            if (mat.HasProperty("_Emission")) mat.SetFloat("_Emission", m_EmissiveStrength);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", m_SurfaceColor);
            if (mat.HasProperty("_HasPortalView")) mat.SetFloat("_HasPortalView", 0f);
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Portals/PortalSwirl");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material material = new Material(shader);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private static void SetMaterialColour(Material material, Color colour, float emission)
        {
            Color opaque = new Color(colour.r, colour.g, colour.b, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", opaque);
            if (material.HasProperty("_Color")) material.SetColor("_Color", opaque);

            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", opaque * emission);
            }
        }

        private static Mesh BuildDisc(Vector2 halfSize, int segments, float zOffset)
        {
            int count = Mathf.Max(8, segments);
            Vector3[] vertices = new Vector3[count + 1];
            int[] triangles = new int[count * 3];

            vertices[0] = new Vector3(0f, 0f, zOffset);
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * halfSize.x,
                    Mathf.Sin(angle) * halfSize.y,
                    zOffset);

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % count + 1;
            }

            Mesh mesh = new Mesh { name = "Portal Disc" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildFrame(Vector2 halfSize, int segments, float thickness, float zOffset)
        {
            int count = Mathf.Max(8, segments);
            Vector3[] vertices = new Vector3[count * 4];
            int[] triangles = new int[count * 6];

            Vector2 outer = new Vector2(halfSize.x + thickness, halfSize.y + thickness);

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                int v = i * 4;

                vertices[v] = new Vector3(c * halfSize.x, s * halfSize.y, zOffset);
                vertices[v + 1] = new Vector3(c * outer.x, s * outer.y, zOffset);

                float nextAngle = (i + 1) / (float)count * Mathf.PI * 2f;
                vertices[v + 2] = new Vector3(Mathf.Cos(nextAngle) * halfSize.x,
                    Mathf.Sin(nextAngle) * halfSize.y, zOffset);
                vertices[v + 3] = new Vector3(Mathf.Cos(nextAngle) * outer.x,
                    Mathf.Sin(nextAngle) * outer.y, zOffset);

                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            Mesh mesh = new Mesh { name = "Portal Frame" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
