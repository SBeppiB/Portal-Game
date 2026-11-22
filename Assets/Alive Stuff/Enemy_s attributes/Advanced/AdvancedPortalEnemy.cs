using Portals.Core;
using Portals.Integration;
using Portals.Traversal;
using UnityEngine;

namespace Portals.Enemy
{
    /// <summary>
    /// Advanced enemy that runs the EXACT player moveset via an AI-controlled
    /// PlayerMovement (dash, slide, wall run, grapple, high jump, momentum, glide, ...)
    /// while the existing EnemyMovement AI decides where to go. It also carries its own
    /// portal gun instance (AI driven, no player input) and traverses portals with full
    /// momentum preservation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMovement))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class AdvancedPortalEnemy : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform m_Player;
        [SerializeField] private PlayerMovement m_PlayerMovementTemplate;

        [Header("Portal Gun (own instance, AI driven)")]
        [SerializeField] private bool m_UsePortalGun = true;
        [SerializeField, Min(0.1f)] private float m_FireCooldown = 2.5f;
        [SerializeField, Min(1f)] private float m_FireRange = 30f;
        [SerializeField, Min(0f)] private float m_AimSmoothing = 8f;
        [SerializeField] private PortalKind m_PortalMode = PortalKind.Locaversal;

        [Header("Player Moveset (AI input)")]
        [SerializeField, Min(0f)] private float m_JumpAssistHeight = 0.8f;
        [SerializeField, Min(1f)] private float m_DashDistance = 8f;
        [SerializeField, Min(1f)] private float m_GrappleDistance = 14f;

        private EnemyMovement m_EnemyAI;
        private PlayerMovement m_PlayerMovement;
        private PlayerMovement.AIPlayerMovementInput m_AIInput;
        private Camera m_AimCamera;
        private PortalPlacer m_Placer;
        private PortalFiringController m_PortalGun;
        private PortalTraverser m_Traverser;
        private float m_NextFireTime;
        private float m_LastDashTapTime;
        private int m_DashTapFrame = -1;
        private readonly RaycastHit[] m_HitBuffer = new RaycastHit[8];

        public PortalFiringController PortalGun => m_PortalGun;
        public PortalTraverser Traverser => m_Traverser;
        public PlayerMovement Movement => m_PlayerMovement;
        public bool HasPortalGun => m_PortalGun != null;
        public float NextFireTime => m_NextFireTime;

        private void Awake()
        {
            m_EnemyAI = GetComponent<EnemyMovement>();

            m_PlayerMovement = GetComponent<PlayerMovement>();
            if (m_PlayerMovement == null) m_PlayerMovement = gameObject.AddComponent<PlayerMovement>();
            m_AIInput = m_PlayerMovement.EnableAIControl();

            // The AI brain decides direction/abilities; PlayerMovement owns the controller.
            m_EnemyAI.usePlayerMovement = true;

            m_Traverser = GetComponent<PortalTraverser>();
            if (m_Traverser == null) m_Traverser = gameObject.AddComponent<PortalTraverser>();
            m_Traverser.AutoDetect = true;

            if (GetComponent<EnemyMomentumReceiver>() == null)
                gameObject.AddComponent<EnemyMomentumReceiver>();
        }

        private void Start()
        {
            if (m_Player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) m_Player = playerObject.transform;
            }

            if (m_PlayerMovementTemplate == null)
            {
                foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
                {
                    if (movement == m_PlayerMovement) continue;
                    m_PlayerMovementTemplate = movement;
                    break;
                }
            }
            if (m_PlayerMovementTemplate != null) CopyPlayerMovementProfile();

            if (m_Player != null && m_EnemyAI != null)
            {
                m_EnemyAI.player = m_Player;
                m_EnemyAI.enabled = true;
            }

            if (m_PlayerMovement.playerCamera == null)
            {
                if (m_AimCamera == null) EnsureAimCamera();
                m_PlayerMovement.playerCamera = m_AimCamera;
            }

            if (m_UsePortalGun) SetupPortalGun();
        }

        /// <summary>Copies the human player's tuning onto the enemy's identical moveset.</summary>
        private void CopyPlayerMovementProfile()
        {
            PlayerMovement p = m_PlayerMovementTemplate;
            if (p == null || m_PlayerMovement == null) return;

            m_PlayerMovement.walkSpeed = p.walkSpeed;
            m_PlayerMovement.runSpeed = p.runSpeed;
            m_PlayerMovement.jumpPower = p.jumpPower;
            m_PlayerMovement.gravity = p.gravity;
            m_PlayerMovement.maxStamina = p.maxStamina;
            m_PlayerMovement.dashSpeed = p.dashSpeed;
            m_PlayerMovement.dashDistance = p.dashDistance;
            m_PlayerMovement.dashCooldown = p.dashCooldown;
            m_PlayerMovement.slideSpeed = p.slideSpeed;
            m_PlayerMovement.airBoostSpeed = p.airBoostSpeed;
            m_PlayerMovement.highJumpMultiplier = p.highJumpMultiplier;
            m_PlayerMovement.grappleRange = p.grappleRange;
            m_PlayerMovement.grappleSpeed = p.grappleSpeed;
            m_PlayerMovement.enableDoubleJump = p.enableDoubleJump;
            m_PlayerMovement.enableAirBoost = p.enableAirBoost;
        }

        private void EnsureAimCamera()
        {
            m_AimCamera = GetComponentInChildren<Camera>();
            if (m_AimCamera != null) return;

            GameObject cameraObject = new GameObject("Enemy Aim Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            m_AimCamera = cameraObject.AddComponent<Camera>();
            m_AimCamera.enabled = false; // only the transform/ray is needed for AI aiming
        }

        private void SetupPortalGun()
        {
            EnsureAimCamera();

            m_Placer = GetComponent<PortalPlacer>();
            if (m_Placer == null) m_Placer = gameObject.AddComponent<PortalPlacer>();
            m_Placer.SetIgnoreRoot(transform);

            // One gun instance per enemy -- never the player's component.
            m_PortalGun = GetComponent<PortalFiringController>();
            if (m_PortalGun == null) m_PortalGun = gameObject.AddComponent<PortalFiringController>();

            m_PortalGun.Configure(m_AimCamera, m_Placer, null, null);
            m_PortalGun.SetAIControlled(true);
            m_PortalGun.SetPairOwner(gameObject); // independent A/B pair for this enemy

            PortalLifecycleManager lifecycle = FindFirstObjectByType<PortalLifecycleManager>();
            if (lifecycle != null) m_PortalGun.ConfigureLifecycle(lifecycle);
            while (m_PortalGun.TravelMode != m_PortalMode) m_PortalGun.ToggleTravelMode();
        }

        private void Update()
        {
            DrivePlayerMoveset();
            AimAndFirePortalGun();
        }

        /// <summary>Translates the AI's desired motion into real player movement input.</summary>
        private void DrivePlayerMoveset()
        {
            if (m_PlayerMovement == null || m_AIInput == null) return;

            Vector3 desired = m_EnemyAI != null ? m_EnemyAI.DesiredVelocity : Vector3.zero;
            Vector3 local = transform.InverseTransformDirection(desired);
            m_AIInput.Horizontal = Mathf.Clamp(local.x / Mathf.Max(1f, m_PlayerMovement.runSpeed), -1f, 1f);
            m_AIInput.Vertical = Mathf.Clamp(local.z / Mathf.Max(1f, m_PlayerMovement.runSpeed), -1f, 1f);
            m_AIInput.RunHeld = desired.magnitude > m_PlayerMovement.walkSpeed * 0.9f;

            if (m_Player == null) return;
            Vector3 toPlayer = m_Player.position - transform.position;
            float distance = toPlayer.magnitude;
            bool grounded = m_PlayerMovement.IsGrounded;

            // Use the same abilities a player would: jump obstacles, dash to close gap,
            // high-jump to reach elevated targets, grapple across long distances, glide down.
            if (grounded && (m_EnemyAI != null && m_EnemyAI.WantsJump ||
                             Physics.Raycast(transform.position + Vector3.up * 0.5f,
                                 transform.forward, 1.2f, ~0, QueryTriggerInteraction.Ignore)))
                m_AIInput.JumpDown = true;

            if (distance > m_DashDistance && m_PlayerMovement.IsAbilityAvailable("dash"))
                TryDash();

            if (toPlayer.y > m_JumpAssistHeight && grounded)
                TriggerHighJump();

            if (distance > m_GrappleDistance && m_PlayerMovement.IsAbilityAvailable("grapple"))
                m_AIInput.GrappleDown = true;

            if (!grounded && m_PlayerMovement.GetCurrentVelocity() < 0f)
                m_AIInput.GlideHeld = true;
            else
                m_AIInput.GlideHeld = false;
        }

        private void TryDash()
        {
            // PlayerMovement dashes on a double-tap of W. Produce that same edge pattern
            // deterministically on two consecutive frames.
            if (Time.frameCount == m_DashTapFrame) return;
            m_DashTapFrame = Time.frameCount;
            m_AIInput.WDown = true;
            if (m_LastDashTapTime > 0f && Time.time - m_LastDashTapTime < 0.2f)
                m_LastDashTapTime = 0f; // second tap consumed by PlayerMovement this frame
            else
                m_LastDashTapTime = Time.time;
        }

        private void TriggerHighJump()
        {
            m_AIInput.HighJumpDown = true;
            m_AIInput.HighJumpHeld = true;
            m_AIInput.HighJumpUp = true;
        }

        private void AimAndFirePortalGun()
        {
            if (m_Player == null || m_PortalGun == null || m_AimCamera == null) return;
            if (Time.time < m_NextFireTime) return;

            Vector3 eye = m_AimCamera.transform.position;
            Vector3 target = m_Player.position + Vector3.up;
            Vector3 toTarget = target - eye;
            float distance = toTarget.magnitude;
            if (distance > m_FireRange || distance < 0.01f) return;

            Vector3 desiredForward = toTarget / distance;
            Vector3 forward = Vector3.Slerp(m_AimCamera.transform.forward, desiredForward,
                Mathf.Clamp01(m_AimSmoothing * Time.deltaTime)).normalized;
            m_AimCamera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (Vector3.Dot(forward, desiredForward) < 0.98f) return;
            if (!HasClearShot(eye, toTarget)) return;

            if (m_PortalGun.AIFire(new Ray(eye, forward)))
                m_NextFireTime = Time.time + m_FireCooldown;
        }

        private bool HasClearShot(Vector3 eye, Vector3 toTarget)
        {
            int count = Physics.RaycastNonAlloc(eye, toTarget.normalized, m_HitBuffer,
                toTarget.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = m_HitBuffer[i].collider != null
                    ? m_HitBuffer[i].collider.transform
                    : null;
                if (hitTransform == null) continue;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }
    }
}
