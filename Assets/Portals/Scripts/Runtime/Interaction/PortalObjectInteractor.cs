using System.Collections;
using Portals.Core;
using Portals.Game;
using Portals.Traversal;
using UnityEngine;

namespace Portals.Interaction
{
    /// <summary>
    /// Picks up, drags, drops and throws compatible objects. Uses a CharacterController
    /// player and a configurable hold point in front of the camera. Objects carried
    /// through a portal are teleported with the player using the existing portal maths.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalObjectInteractor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode m_InteractKey = KeyCode.E;

        [Header("Interaction")]
        [SerializeField, Min(0.1f)] private float m_Range = 5f;
        [SerializeField, Min(0.1f)] private float m_HoldDistance = 2f;
        [SerializeField, Min(0f)] private float m_ThrowForce = 12f;
        [SerializeField, Min(0.05f)] private float m_DoubleClickWindow = 0.3f;

        [Header("References")]
        [SerializeField] private Camera m_Camera;
        [SerializeField] private PortalTraverser m_Traverser;
        [SerializeField, Tooltip("Own colliders (e.g. the player's CharacterController) are ignored.")]
        private Transform m_IgnoreRoot;

        private readonly RaycastHit[] m_HitBuffer = new RaycastHit[16];
        private readonly RaycastHit[] m_SphereBuffer = new RaycastHit[16];

        private GameObject m_HeldObject;
        private Rigidbody m_HeldBody;
        private Vector3 m_HeldBodyOriginalVelocity;
        private bool m_HeldBodyOriginalUseGravity;
        private float m_LastClickTime;
        private float m_PendingDropTime = -1f;

        public GameObject HeldObject => m_HeldObject;
        public bool IsHolding => m_HeldObject != null;
        public float Range => m_Range;
        public float HoldDistance => m_HoldDistance;
        public float ThrowForce => m_ThrowForce;
        public float DoubleClickWindow => m_DoubleClickWindow;

        private void Awake()
        {
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Traverser == null) m_Traverser = GetComponent<PortalTraverser>();
            if (m_IgnoreRoot == null) m_IgnoreRoot = transform;
            if (m_Traverser != null)
                m_Traverser.Traversed += OnTraversed;
        }

        private void OnDestroy()
        {
            if (m_Traverser != null) m_Traverser.Traversed -= OnTraversed;
        }

        private void Update()
        {
            if (GameplayInput.GetKeyDown(m_InteractKey)) HandleInteractInput();

            if (m_HeldObject != null && m_PendingDropTime >= 0f &&
                Time.time - m_PendingDropTime >= m_DoubleClickWindow)
            {
                DropHeld();
                m_PendingDropTime = -1f;
            }
        }

        private void FixedUpdate()
        {
            if (m_HeldObject == null || m_HeldBody == null || m_Camera == null) return;

            Vector3 holdPosition = m_Camera.transform.position +
                                   m_Camera.transform.forward * m_HoldDistance;

            // Prevent the object clipping through walls between the camera and the hold
            // point. Own colliders are skipped so a carried object never snaps to the player.
            int count = Physics.SphereCastNonAlloc(m_Camera.transform.position, 0.25f,
                m_Camera.transform.forward, m_SphereBuffer, m_HoldDistance,
                ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = m_SphereBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || IsIgnored(hitCollider.transform)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
            }
            if (nearest < float.MaxValue)
                holdPosition = m_Camera.transform.position +
                               m_Camera.transform.forward * Mathf.Max(0.1f, nearest - 0.25f);

            Vector3 velocity = (holdPosition - m_HeldBody.position) * 10f;
            m_HeldBody.linearVelocity = velocity;
            m_HeldBody.MovePosition(m_HeldBody.position + velocity * Time.fixedDeltaTime);
            m_HeldBody.MoveRotation(Quaternion.Slerp(m_HeldBody.rotation,
                m_Camera.transform.rotation, 10f * Time.fixedDeltaTime));
        }

        /// <summary>Public entry point used by tests and custom input systems.</summary>
        public void Interact() => HandleInteractInput();

        private void HandleInteractInput()
        {
            if (m_HeldObject != null)
            {
                float sinceLast = Time.time - m_LastClickTime;
                if (sinceLast <= m_DoubleClickWindow && m_PendingDropTime < 0f)
                {
                    // Second click within the window: throw instead of drop.
                    ThrowHeld();
                    m_PendingDropTime = -1f;
                    m_LastClickTime = Time.time;
                    return;
                }

                // First click while holding: schedule a drop (cancelled by a throw).
                m_PendingDropTime = Time.time;
                m_LastClickTime = Time.time;
                return;
            }

            // Not holding: try to pick up.
            m_LastClickTime = Time.time;
            TryPickup();
        }

        private bool IsIgnored(Transform candidate) =>
            m_IgnoreRoot != null && candidate != null &&
            (candidate == m_IgnoreRoot || candidate.IsChildOf(m_IgnoreRoot));

        private void TryPickup()
        {
            if (m_Camera == null) return;
            Ray ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, m_HitBuffer, m_Range, ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            RaycastHit chosen = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = m_HitBuffer[i];
                if (hit.collider == null || IsIgnored(hit.collider.transform)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                chosen = hit;
                found = true;
            }
            if (!found) return;

            GameObject target = chosen.collider.gameObject;
            Rigidbody body = target.GetComponentInParent<Rigidbody>();
            if (body == null) return;

            InteractableObject interactable = chosen.collider.GetComponentInParent<InteractableObject>();
            if (interactable != null && !interactable.PickupCompatible) return;
            target = body.gameObject;

            m_HeldObject = target;
            m_HeldBody = body;
            m_HeldBodyOriginalUseGravity = body.useGravity;
            m_HeldBodyOriginalVelocity = body.linearVelocity;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void DropHeld()
        {
            if (m_HeldBody == null) return;
            m_HeldBody.useGravity = m_HeldBodyOriginalUseGravity;
            m_HeldBody.linearVelocity = Vector3.zero;
            m_HeldBody.constraints = RigidbodyConstraints.None;
            m_HeldObject = null;
            m_HeldBody = null;
        }

        private void ThrowHeld()
        {
            if (m_HeldBody == null || m_Camera == null) return;
            m_HeldBody.useGravity = m_HeldBodyOriginalUseGravity;
            m_HeldBody.constraints = RigidbodyConstraints.None;
            m_HeldBody.linearVelocity = m_Camera.transform.forward * m_ThrowForce;
            m_HeldObject = null;
            m_HeldBody = null;
        }

        /// <summary>
        /// Detaches the currently held object so it can be moved into inventory. Restores
        /// its physics state first; the caller deactivates/parents it. Returns null when
        /// nothing is held.
        /// </summary>
        public GameObject ReleaseHeldForStorage()
        {
            GameObject obj = m_HeldObject;
            if (obj == null) return null;

            if (m_HeldBody != null)
            {
                m_HeldBody.useGravity = m_HeldBodyOriginalUseGravity;
                m_HeldBody.constraints = RigidbodyConstraints.None;
                m_HeldBody.linearVelocity = Vector3.zero;
                m_HeldBody.angularVelocity = Vector3.zero;
            }

            m_HeldObject = null;
            m_HeldBody = null;
            m_PendingDropTime = -1f;
            return obj;
        }

        public void DropFromInventory(GameObject obj, Vector3 position)
        {
            if (obj == null) return;
            obj.SetActive(true);
            obj.transform.position = position;
            Rigidbody body = obj.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
            }
        }

        private void OnTraversed(PortalTraversalInfo info)
        {
            if (m_HeldObject == null || info.Exit == null) return;

            Vector3 pos = m_HeldObject.transform.position;
            Quaternion rot = m_HeldObject.transform.rotation;
            Vector3 vel = m_HeldBody != null ? m_HeldBody.linearVelocity : Vector3.zero;

            info.Entrance.ComputeExitPose(info.Exit, pos, rot, false, out Vector3 exitPos, out Quaternion exitRot);
            info.Entrance.ComputeExitMotion(info.Exit, vel, Vector3.zero, false, out Vector3 exitVel, out _);

            m_HeldObject.transform.SetPositionAndRotation(exitPos, exitRot);
            if (m_HeldBody != null) m_HeldBody.linearVelocity = exitVel;
        }
    }
}
