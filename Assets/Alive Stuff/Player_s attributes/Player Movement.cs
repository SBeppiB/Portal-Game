using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals.Traversal;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour, IPortalMomentumReceiver
{
    /// <summary>
    /// Abstraction over the raw Unity input reads below. The player uses the default
    /// implementation; AI-controlled enemies supply an AIPlayerMovementInput so they run
    /// this exact same moveset (dash, slide, wall run, grapple, high jump, momentum, ...).
    /// </summary>
    public interface IPlayerMovementInput
    {
        float Horizontal { get; }
        float Vertical { get; }
        bool RunHeld { get; }
        bool CrouchHeld { get; }
        bool CrouchDown { get; }
        bool JumpDown { get; }
        bool WDown { get; }
        bool ADown { get; }
        bool SDown { get; }
        bool DDown { get; }
        bool WHeld { get; }
        bool AHeld { get; }
        bool SHeld { get; }
        bool DHeld { get; }
        bool HighJumpDown { get; }
        bool HighJumpHeld { get; }
        bool HighJumpUp { get; }
        bool GrappleDown { get; }
        bool GlideDown { get; }
        bool GlideHeld { get; }
        float MouseX { get; }
        float MouseY { get; }
    }

    public sealed class UnityPlayerMovementInput : IPlayerMovementInput
    {
        public float Horizontal => Input.GetAxis("Horizontal");
        public float Vertical => Input.GetAxis("Vertical");
        public bool RunHeld => Input.GetKey(KeyCode.LeftShift);
        public bool CrouchHeld => Input.GetKey(KeyCode.LeftControl);
        public bool CrouchDown => Input.GetKeyDown(KeyCode.LeftControl);
        public bool JumpDown => Input.GetButtonDown("Jump");
        public bool WDown => Input.GetKeyDown(KeyCode.W);
        public bool ADown => Input.GetKeyDown(KeyCode.A);
        public bool SDown => Input.GetKeyDown(KeyCode.S);
        public bool DDown => Input.GetKeyDown(KeyCode.D);
        public bool WHeld => Input.GetKey(KeyCode.W);
        public bool AHeld => Input.GetKey(KeyCode.A);
        public bool SHeld => Input.GetKey(KeyCode.S);
        public bool DHeld => Input.GetKey(KeyCode.D);
        public bool HighJumpDown => Input.GetKeyDown(KeyCode.F);
        public bool HighJumpHeld => Input.GetKey(KeyCode.F);
        public bool HighJumpUp => Input.GetKeyUp(KeyCode.F);
        public bool GrappleDown => Input.GetKeyDown(KeyCode.R);
        public bool GlideDown => Input.GetKeyDown(KeyCode.V);
        public bool GlideHeld => Input.GetKey(KeyCode.V);
        public float MouseX => Input.GetAxis("Mouse X");
        public float MouseY => Input.GetAxis("Mouse Y");
    }

    /// <summary>Writable input state for AI-controlled copies of the player moveset.</summary>
    public sealed class AIPlayerMovementInput : IPlayerMovementInput
    {
        public float Horizontal { get; set; }
        public float Vertical { get; set; }
        public bool RunHeld { get; set; }
        public bool CrouchHeld { get; set; }
        public bool CrouchDown { get; set; }
        public bool JumpDown { get; set; }
        public bool WDown { get; set; }
        public bool ADown { get; set; }
        public bool SDown { get; set; }
        public bool DDown { get; set; }
        public bool WHeld { get; set; }
        public bool AHeld { get; set; }
        public bool SHeld { get; set; }
        public bool DHeld { get; set; }
        public bool HighJumpDown { get; set; }
        public bool HighJumpHeld { get; set; }
        public bool HighJumpUp { get; set; }
        public bool GrappleDown { get; set; }
        public bool GlideDown { get; set; }
        public bool GlideHeld { get; set; }
        public float MouseX { get; set; }
        public float MouseY { get; set; }

        public void ClearFrameTriggers()
        {
            CrouchDown = JumpDown = WDown = ADown = SDown = DDown = false;
            HighJumpDown = HighJumpUp = GrappleDown = GlideDown = false;
        }
    }

    private static readonly IPlayerMovementInput s_UnityInput = new UnityPlayerMovementInput();
    [SerializeField, Tooltip("AI-controlled copies are driven by AIPlayerMovementInput instead of Unity input.")]
    private bool m_AIControlled = false;

    /// <summary>Overrides the input source; null uses real player input.</summary>
    public IPlayerMovementInput MovementInput { get; set; }
    private IPlayerMovementInput MoveInput => MovementInput ?? s_UnityInput;
    public bool AIControlled => m_AIControlled || MovementInput is AIPlayerMovementInput;

    /// <summary>Switches this movement component to AI control and returns its input state.</summary>
    public AIPlayerMovementInput EnableAIControl()
    {
        AIPlayerMovementInput ai = MovementInput as AIPlayerMovementInput;
        if (ai == null)
        {
            ai = new AIPlayerMovementInput();
            MovementInput = ai;
        }
        m_AIControlled = true;
        return ai;
    }
    // =====================================================================
    // INSPECTOR HEADERS
    // =====================================================================

    [Header("Camera")]
    public Camera playerCamera;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;

    [Header("Basic Movement")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 20f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;

    [Header("Sound Effects")]
    public AudioClip dashSFX;
    public AudioClip[] dashSFXVariations;
    public AudioClip slideSFX;
    public AudioClip[] slideSFXVariations;
    public AudioClip airBoostSFX;
    public AudioClip[] airBoostSFXVariations;
    public AudioClip downslamSFX;
    public AudioClip[] downslamSFXVariations;
    public AudioClip slamImpactSFX;
    public AudioClip[] slamImpactSFXVariations;
    public AudioClip jumpSFX;
    public AudioClip[] jumpSFXVariations;
    public AudioClip dashJumpSFX;
    public AudioClip[] dashJumpSFXVariations;
    public AudioClip slideJumpSFX;
    public AudioClip[] slideJumpSFXVariations;
    public AudioClip lowStaminaSFX;
    public AudioClip[] lowStaminaSFXVariations;
    public AudioClip doubleJumpSFX;
    public AudioClip[] doubleJumpSFXVariations;
    public AudioClip rollSFX;
    public AudioClip[] rollSFXVariations;
    public AudioClip airDashSFX;
    public AudioClip[] airDashSFXVariations;
    public AudioClip ledgeGrabSFX;
    public AudioClip[] ledgeGrabSFXVariations;
    public AudioClip glideSFX;
    public AudioClip[] glideSFXVariations;
    public AudioClip highJumpChargeSFX;
    public AudioClip[] highJumpChargeSFXVariations;
    public AudioClip footstepSFX;
    public AudioClip[] footstepSFXVariations;
    public AudioClip landingSFX;
    public AudioClip[] landingSFXVariations;
    public AudioClip wallRunSFX;
    public AudioClip[] wallRunSFXVariations;
    public AudioClip wallClimbSFX;
    public AudioClip[] wallClimbSFXVariations;
    public AudioClip wallHopSFX;
    public AudioClip[] wallHopSFXVariations;
    public AudioClip fallDamageClip;
    public AudioClip[] fallDamageClipVariations;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float runStaminaDrain = 15f;
    public float dashStaminaCost = 25f;
    public float slideStaminaCost = 20f;
    public float airBoostStaminaCost = 20f;
    public float dashJumpStaminaCost = 10f;
    public float doubleJumpStaminaCost = 10f;
    public float highJumpStaminaCost = 15f;
    public float rollStaminaCost = 10f;
    public float airDashStaminaCost = 20f;
    public float staminaRegenRate = 20f;
    public float staminaRegenDelay = 1.5f;
    public float lowStaminaThreshold = 20f;
    public float lowStaminaSpeedMultiplier = 0.6f;

    [Header("Slide Settings")]
    public float slideSpeed = 18f;
    public float slideDeceleration = 1.5f;
    public float slideMinSpeed = 4f;
    public float slideDuration = 5f;
    public float slideControlStrength = 0.3f;
    public float slideCooldown = 0.5f;
    public float slideJumpSpeedMultiplier = 1.2f;

    [Header("Dash Settings")]
    public float dashDistance = 8f;
    public float dashSpeed = 30f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float invincibilityDuration = 0.1f;
    public float dashJumpBufferTime = 0.15f;
    public float dashJumpSpeedMultiplier = 1.5f;

    [Header("Advanced Momentum Settings")]
    public bool enableAdvancedMomentum = true;
    public float momentumDecayRate = 0.8f;
    public float airMomentumDecay = 0.95f;
    public float groundMomentumDecay = 0.85f;
    public float minMomentumSpeed = 0.5f;
    public float playerControlDuringMomentum = 0.3f;
    public float momentumMaxSpeed = 30f;
    public float momentumDirectionalControl = 0.5f;
    public float momentumAcceleration = 1.2f;
    public float momentumAnglePreservation = 0.9f;
    public float momentumTurnDeceleration = 0.7f;
    public float sharpTurnAngleThreshold = 90f;
    public bool enableMomentumStacking = true;
    public bool enableEnergyConservation = true;
    public float energyConservationLoss = 0.8f;
    public float momentumRedirectDamping = 0.6f;

    [Header("Air Boost Settings")]
    public bool enableAirBoost = true;
    public float airBoostSpeed = 15f;
    public float airBoostDuration = 0.3f;
    public float airBoostCooldown = 1f;

    [Header("Downslam Settings")]
    public float slamSpeed = 25f;
    public float slamDamage = 50f;
    public float slamKnockbackForce = 10f;
    public float slamRadius = 5f;
    public float slamActivationDelay = 0.2f;

    [Header("High Jump Settings")]
    public float highJumpMultiplier = 1.5f;
    public float highJumpChargePerSecond = 1f;
    public float maxHighJumpChargeTime = 8f;

    [Header("Wall Interaction Settings")]
    public float wallDetectionDistance = 0.5f;
    public float wallHopForce = 10f;
    public float wallClimbSpeed = 3f;
    public float wallRunSpeed = 8f;
    public float wallRunGravityReduction = 0.3f;
    public float wallRunMinAngle = 75f;
    public float wallRunMaxAngle = 105f;
    public LayerMask wallLayer = 1 << 3;
    public float wallClingGravityReduction = 0.1f;

    [Header("Grapple Settings")]
    public float grappleRange = 30f;
    public float grappleSpeed = 25f;
    public float grapplePullSpeed = 20f;
    public LayerMask heavyLayer = -1;
    public LayerMask lightLayer = -1;
    public GameObject grappleHookPrefab;

    [Header("Double Jump Settings")]
    public bool enableDoubleJump = true;
    public float doubleJumpPower = 7f;
    public bool requiresAirMagic = false;

    [Header("Roll Settings")]
    public float rollDistance = 5f;
    public float rollSpeed = 20f;
    public float rollDuration = 0.3f;
    public float rollCooldown = 0.5f;
    public float rollFallDamageReduction = 0.6f;
    public float rollFallDamageBaseHeight = 12f;
    public float rollFallDamageReductionPer12M = 0.2f;

    [Header("Air Dash Settings")]
    public int maxAirDashes = 5;
    public float airDashCooldown = 0.2f;

    [Header("Ledge Grab Settings")]
    public float ledgeDetectionDistance = 0.5f;
    public float ledgeDetectionHeight = 0.3f;
    public float ledgeClimbSpeed = 3f;
    public float ledgeGrabCooldown = 0.5f;

    [Header("Glide Settings")]
    public float glideGravityReduction = 0.3f;
    public float glideSpeedMultiplier = 1.5f;
    public float glideStaminaDrain = 5f;

    [Header("Vault Settings")]
    public float vaultDetectionDistance = 1f;
    public float vaultHeight = 1.5f;
    public float vaultSpeed = 8f;

    [Header("Air Control Settings")]
    public float airControlStrength = 0.4f;
    public float airTurnSpeed = 5f;
    public float maxAirSpeed = 20f;

    [Header("Velocity Tracking")]
    public bool enableVelocityTracking = true;

    [Header("Ground Friction")]
    public float groundFriction = 0.95f;
    public float slideFriction = 0.92f;
    public float iceSlipperiness = 0.98f;

    [Header("Knockback System")]
    public bool enableKnockback = true;
    public float knockbackResistance = 0.5f;

    [Header("Fall Damage System")]
    public bool enableFallDamage = true;
    public float fallDamageThreshold = 15f;
    public float fallDamagePerUnit = 5f;

    [Header("Sprint Settings")]
    public bool enableSprinting = true;
    public float sprintSpeedMultiplier = 1.5f;
    public float sprintStaminaCost = 30f;
    public float sprintCooldown = 0.5f;

    [Header("Crouch Settings")]
    public bool enableCrouch = true;
    public float crouchSpeed = 3f;
    public float crouchStaminaCost = 5f;
    public float crouchColliderHeight = 1f;

    [Header("Footstep System")]
    public float footstepInterval = 0.4f;
    public float footstepIntervalWhileRunning = 0.25f;

    [Header("Landing Effects")]
    public bool enableLandingEffects = true;
    public float landingCameraShakeMagnitude = 0.1f;
    public float landingCameraShakeDuration = 0.1f;

    [Header("Exhaustion System")]
    public bool enableExhaustion = true;
    public float exhaustionThreshold = 0.2f;
    public float exhaustionSpeedReduction = 0.4f;
    public float exhaustionRecoveryTime = 3f;

    [Header("Safe Zone")]
    public Color safeZoneIndicatorColor = Color.green;

    [Header("Respawn")]
    [Tooltip("When the player is touched by an enemy, they are teleported here. Assign an object named 'SpawnPoint' or drag your spawn transform.")]
    public Transform spawnPoint;
    [Tooltip("Height in meters above the spawn point when respawning (e.g. 20 = drop in from above).")]
    public float respawnHeightAboveSpawn = 20f;

    // =====================================================================
    // PRIVATE REFERENCES
    // =====================================================================

    private CharacterController characterController;
    private Transform cachedTransform;
    private AudioSource audioSource;
    private AudioSource loopingAudioSource;
    private LineRenderer grappleLine;

    // =====================================================================
    // MOVEMENT STATE
    // =====================================================================

    public enum MovementState { Idle, Walking, Running, Sprinting, Dashing, Sliding, Jumping, WallRunning, Falling, Crouching }
    private MovementState currentMovementState = MovementState.Idle;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0f;
    private bool canMove = true;

    // =====================================================================
    // STAMINA STATE
    // =====================================================================

    private float lastStaminaUse = 0f;
    private bool lowStaminaSoundPlayed = false;

    // =====================================================================
    // MOMENTUM STATE
    // =====================================================================

    private Vector3 dashMomentum = Vector3.zero;
    private Vector3 slideMomentum = Vector3.zero;
    private Vector3 jumpMomentum = Vector3.zero;
    private Vector3 wallRunMomentum = Vector3.zero;
    private Vector3 combinedMomentum = Vector3.zero;
    private bool hasMomentum = false;
    private Vector3 momentumHorizontal = Vector3.zero;
    private Vector3 previousMomentumDirection = Vector3.zero;
    private float momentumDuration = 0f;
    private int activeMomentumSources = 0;

    // =====================================================================
    // ABILITY STATE FLAGS
    // =====================================================================

    private bool isDashing = false;
    private bool isSliding = false;
    private bool isAirBoosting = false;
    private bool isDownslamming = false;
    private bool isChargingHighJump = false;
    private bool isWallClinging = false;
    private bool isWallClimbing = false;
    private bool isWallRunning = false;
    private bool isGrappling = false;
    private bool hasDoubleJumped = false;
    private bool isRolling = false;
    private bool isLedgeGrabbing = false;
    private bool isGliding = false;
    private bool isVaulting = false;
    private bool isSprinting = false;
    private bool isCrouching = false;
    private bool isExhausted = false;
    private bool isInvincible = false;
    private bool isDashJump = false;
    private bool isInSafeZone = false;
    private bool wasOnWall = false;
    private bool wasGrounded = true;
    private bool isFalling = false;

    // =====================================================================
    // TIMERS & COOLDOWNS
    // =====================================================================

    private float dashTimer = 0f;
    private float lastDashTime = -99f;
    private float dashEndTime = -99f;

    private float slideTimer = 0f;
    private float lastSlideTime = -99f;
    private float currentSlideSpeed = 0f;

    private float airBoostTimer = 0f;
    private float lastAirBoostTime = -99f;

    private float highJumpChargeTime = 0f;

    private float lastWallInteractionTime = -99f;
    private float lastSpaceTap = -99f;

    private float rollTimer = 0f;
    private float lastRollTime = -99f;

    private float lastLedgeGrabTime = -99f;

    private float timeInAir = 0f;

    private float lastSprintTime = -99f;

    private float exhaustionEndTime = 0f;

    private float nextFootstepTime = 0f;

    private float fallStartHeight = 0f;
    private float lastGroundHeight = 0f;
    private float totalFallDistance = 0f;

    private int currentAirDashCount = 0;
    private float lastAirDashTime = -99f;

    // =====================================================================
    // ABILITY VECTORS
    // =====================================================================

    private Vector3 dashDirection = Vector3.zero;
    private Vector3 slideDirection = Vector3.zero;
    private Vector3 airBoostDirection = Vector3.zero;
    private Vector3 rollDirection = Vector3.zero;
    private Vector3 ledgePosition = Vector3.zero;
    private Vector3 ledgeNormal = Vector3.zero;
    private Vector3 wallNormal = Vector3.zero;
    private Vector3 wallPoint = Vector3.zero;
    private Vector3 grappleTarget = Vector3.zero;
    private Vector3 vaultTarget = Vector3.zero;
    private Vector3 knockbackVelocity = Vector3.zero;

    private Transform grappleTargetTransform = null;
    private bool isGrappleHeavy = false;
    private Vector3 grappleDirection = Vector3.zero;
    private float grappleDistance = 0f;
    private float knockbackEndTime = 0f;

    // =====================================================================
    // DOUBLE-TAP DETECTION
    // Separate sets for ground and air to prevent conflicts
    // =====================================================================

    private float groundLastWTap = -99f;
    private float groundLastATap = -99f;
    private float groundLastSTap = -99f;
    private float groundLastDTap = -99f;

    private float airLastWTap = -99f;
    private float airLastATap = -99f;
    private float airLastSTap = -99f;
    private float airLastDTap = -99f;

    private float rollLastWTap = -99f;
    private float rollLastATap = -99f;
    private float rollLastSTap = -99f;
    private float rollLastDTap = -99f;

    private float lastCtrlTap = -99f;
    private const float doubleTapWindow = 0.3f;

    // =====================================================================
    // PER-FRAME CACHED VALUES
    // =====================================================================

    private float deltaTime;
    private float currentTime;
    private bool isGrounded;
    private float horizontalInput;
    private float verticalInput;
    private Vector3 forward;
    private Vector3 right;
    private Vector3 inputDir = Vector3.zero;
    private Vector3 tempVector = Vector3.zero;

    // =====================================================================
    // VELOCITY TRACKING
    // =====================================================================

    private float currentVelocity = 0f;
    private float maxVelocityReached = 0f;

    // =====================================================================
    // SYSTEMS
    // =====================================================================

    private Queue<string> inputBuffer = new Queue<string>();
    private Dictionary<string, float> abilityCooldowns = new Dictionary<string, float>();

    // =====================================================================
    // CONSTANTS
    // =====================================================================

    private const float heightLerpSpeed = 10f;
    private const float groundedYVelocity = -2f;
    private const float inputDeadzone = 0.1f;

    // =====================================================================
    // UNITY LIFECYCLE
    // =====================================================================

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cachedTransform = transform;

        if (!AIControlled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        currentStamina = maxStamina;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        loopingAudioSource = gameObject.AddComponent<AudioSource>();
        loopingAudioSource.playOnAwake = false;
        loopingAudioSource.loop = true;
        loopingAudioSource.spatialBlend = 0f;

        if (spawnPoint == null)
        {
            GameObject found = GameObject.Find("SpawnPoint");
            if (found != null) spawnPoint = found.transform;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (AIControlled || hit.collider == null) return;

        EnemyMovement enemy = hit.collider.GetComponentInParent<EnemyMovement>();
        if (enemy == null) return;

        TeleportToSpawn();
    }

    public void TeleportToSpawn()
    {
        Transform target = spawnPoint != null ? spawnPoint : (GameObject.Find("SpawnPoint")?.transform);
        if (target == null) return;

        characterController.enabled = false;
        cachedTransform.position = target.position + Vector3.up * respawnHeightAboveSpawn;
        cachedTransform.rotation = target.rotation;
        characterController.enabled = true;
    }

    void Update()
    {
        // Cache per-frame values once
        deltaTime = Time.deltaTime;
        currentTime = Time.time;
        isGrounded = characterController.isGrounded;
        horizontalInput = MoveInput.Horizontal;
        verticalInput = MoveInput.Vertical;

        HandleMouseLook();
        UpdateFallTracking();
        UpdateExhaustion();
        UpdateFootsteps();
        UpdateMovementState();

        timeInAir = isGrounded ? 0f : timeInAir + deltaTime;

        // Priority movement states — return early to prevent overlap
        if (isDashing)     { HandleDash();     return; }
        if (isAirBoosting) { HandleAirBoost(); return; }
        if (isDownslamming){ HandleDownslam(); return; }
        if (isSliding)     { HandleSlide();    return; }
        if (isGrappling)   { HandleGrapple();  return; }
        if (isLedgeGrabbing){ HandleLedgeGrab(); return; }
        if (isRolling)     { HandleRoll();     return; }

        HandleNormalMovement();

        // Ability checks
        CheckForDash();
        CheckForAirDash();
        CheckForSlide();
        CheckForAirBoostAndSlam();
        CheckForHighJump();
        CheckForWallInteractions();
        CheckForGrapple();
        CheckForDoubleJump();
        CheckForRoll();
        CheckForLedgeGrab();
        CheckForGlide();

        // Stamina regen
        float timeSinceStaminaUse = currentTime - lastStaminaUse;
        if (timeSinceStaminaUse > staminaRegenDelay && currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(currentStamina + staminaRegenRate * deltaTime, maxStamina);

            if (currentStamina > lowStaminaThreshold)
                lowStaminaSoundPlayed = false;
        }

        // Reset air dash count on ground
        if (isGrounded)
        {
            currentAirDashCount = 0;
        }

        // AI trigger flags are edge-triggered and must be consumed each frame.
        if (MovementInput is AIPlayerMovementInput ai) ai.ClearFrameTriggers();
    }

    
[System.Serializable]
public class MomentumState
{
    // ---- current state ----
    public Vector3 velocity          = Vector3.zero;  // the actual momentum vector
    public float   speed             = 0f;            // cached magnitude
    public float   chainMultiplier   = 1f;            // builds with each chained move
    public int     chainCount        = 0;             // how many moves chained
    public float   lastChainTime     = -99f;          // when last chain link happened
    public bool    active            = false;

    // ---- chain window ----
    // if the player chains a move within this window, multiplier stacks
    public float chainWindowDuration = 0.6f;

    // ---- per-chain multiplier gain ----
    public float chainMultiplierGain = 0.18f;         // +18% per chain link
    public float maxChainMultiplier  = 2.8f;          // cap at 2.8x base speed

    // ---- turn cost ----
    // angle between old and new direction bleeds speed
    // 0° = free, 90° = heavy bleed, 180° = full stop
    public float sharpTurnThreshold  = 60f;           // degrees before bleed kicks in
    public float turnBleedRate       = 0.65f;         // speed kept on a 90° redirect
    public float hardStopAngle       = 150f;          // angle that kills momentum entirely

    // ---- decay ----
    public float groundDecay         = 0.82f;         // per-frame multiplier on ground
    public float airDecay            = 0.97f;         // much slower decay in air
    public float minActiveSpeed      = 1.2f;          // below this, momentum dies

    // ---- landing conversion ----
    // when landing from a fall with a roll/slide, fall speed converts
    public float landingConversionRate = 0.75f;       // 75% of fall speed → ground speed
    public float minFallSpeedToConvert = 8f;          // minimum fall speed to trigger
    public float landingConversionWindow = 0.15f;     // seconds after landing to accept roll/slide
}

    // =====================================================================
    // NORMAL MOVEMENT
    // =====================================================================

    void HandleNormalMovement()
    {
        forward = cachedTransform.TransformDirection(Vector3.forward);
        right = cachedTransform.TransformDirection(Vector3.right);

        bool wantsToRun = MoveInput.RunHeld;
        bool canRun = wantsToRun && currentStamina > 0f;
        bool isMoving = verticalInput != 0f || horizontalInput != 0f;

        if (canRun && isMoving)
        {
            UseStamina(runStaminaDrain * deltaTime);
        }

        float speedMultiplier = currentStamina < lowStaminaThreshold ? lowStaminaSpeedMultiplier : 1f;

        if (currentStamina < lowStaminaThreshold && !lowStaminaSoundPlayed)
        {
            PlayRandomOneShot(lowStaminaSFXVariations, lowStaminaSFX);
            lowStaminaSoundPlayed = true;
        }

        float speedMultiplierExhaustion = isExhausted ? exhaustionSpeedReduction : 1f;
        float baseSpeed = (canRun ? runSpeed : walkSpeed) * speedMultiplier * speedMultiplierExhaustion;

        if (!canMove) baseSpeed = 0f;

        float curSpeedX = baseSpeed * verticalInput;
        float curSpeedY = baseSpeed * horizontalInput;

        tempVector.x = (forward.x * curSpeedX) + (right.x * curSpeedY);
        tempVector.y = 0f;
        tempVector.z = (forward.z * curSpeedX) + (right.z * curSpeedY);
        Vector3 desiredMovement = tempVector;

        if (hasMomentum)
        {
            momentumHorizontal.x = dashMomentum.x;
            momentumHorizontal.y = 0f;
            momentumHorizontal.z = dashMomentum.z;
            float currentMomentumSpeed = momentumHorizontal.magnitude;

            float decayRate = isGrounded ? groundMomentumDecay : airMomentumDecay;
            dashMomentum.x *= decayRate;
            dashMomentum.z *= decayRate;

            if (currentMomentumSpeed < minMomentumSpeed)
            {
                hasMomentum = false;
                dashMomentum = Vector3.zero;
            }
            else
            {
                desiredMovement = Vector3.Lerp(momentumHorizontal, desiredMovement, playerControlDuringMomentum);
            }
        }

        float movementDirectionY = moveDirection.y;
        moveDirection = desiredMovement;

        bool canJump = isGrounded || (currentTime - dashEndTime < dashJumpBufferTime);

        if (MoveInput.JumpDown && canMove && canJump && !isChargingHighJump)
        {
            moveDirection.y = jumpPower;
            PlayRandomJumpSFX();
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        // Apply gravity
        if (!isGrounded && !isGliding)
        {
            moveDirection.y -= gravity * deltaTime;
        }
        else if (isGliding)
        {
            moveDirection.y -= gravity * glideGravityReduction * deltaTime;
        }

        // Crouch collider
        bool crouchInput = MoveInput.CrouchHeld && !MoveInput.RunHeld && isGrounded && !isRolling;
        float targetHeight = crouchInput ? crouchHeight : defaultHeight;
        if (!isSliding && !isRolling)
        {
            characterController.height = Mathf.Lerp(characterController.height, targetHeight, deltaTime * heightLerpSpeed);
        }

        characterController.Move(moveDirection * deltaTime);
    }

    // =====================================================================
    // AIR BOOST
    // =====================================================================

    void CheckForAirBoostAndSlam()
    {
        if (!MoveInput.CrouchDown || isGrounded) return;

        if (currentTime - lastCtrlTap < doubleTapWindow)
        {
            StartDownslam();
        }
        else if (enableAirBoost && currentTime - lastAirBoostTime > airBoostCooldown)
        {
            StartAirBoost();
        }

        lastCtrlTap = currentTime;
    }

    void StartAirBoost()
    {
        if (!UseStamina(airBoostStaminaCost)) return;

        isAirBoosting = true;
        airBoostTimer = 0f;
        lastAirBoostTime = currentTime;

        inputDir.x = horizontalInput;
        inputDir.y = 0f;
        inputDir.z = verticalInput;

        airBoostDirection = inputDir.magnitude < inputDeadzone
            ? cachedTransform.forward
            : cachedTransform.TransformDirection(inputDir).normalized;

        PlayRandomOneShot(airBoostSFXVariations, airBoostSFX);
    }

    void HandleAirBoost()
    {
        airBoostTimer += deltaTime;

        if (airBoostTimer >= airBoostDuration || isGrounded)
        {
            isAirBoosting = false;
            return;
        }

        tempVector = airBoostDirection * (airBoostSpeed * deltaTime);
        moveDirection.y -= gravity * deltaTime;
        tempVector.y = moveDirection.y * deltaTime;
        characterController.Move(tempVector);
    }

    // =====================================================================
    // DASH (GROUND)
    // =====================================================================

    void CheckForDash()
    {
        if (!isGrounded || currentTime - lastDashTime < dashCooldown) return;

        if (MoveInput.WDown)
        {
            if (currentTime - groundLastWTap < doubleTapWindow) StartDash(cachedTransform.forward);
            groundLastWTap = currentTime;
        }
        if (MoveInput.ADown)
        {
            if (currentTime - groundLastATap < doubleTapWindow) StartDash(-cachedTransform.right);
            groundLastATap = currentTime;
        }
        if (MoveInput.SDown)
        {
            if (currentTime - groundLastSTap < doubleTapWindow) StartDash(-cachedTransform.forward);
            groundLastSTap = currentTime;
        }
        if (MoveInput.DDown)
        {
            if (currentTime - groundLastDTap < doubleTapWindow) StartDash(cachedTransform.right);
            groundLastDTap = currentTime;
        }
    }

    void StartDash(Vector3 direction)
    {
        if (!UseStamina(dashStaminaCost)) return;

        isDashing = true;
        dashTimer = 0f;
        dashDirection = direction.normalized;
        lastDashTime = currentTime;
        isInvincible = true;
        isDashJump = false;

        hasMomentum = false;
        dashMomentum = Vector3.zero;

        if (isGrounded) dashDirection.y = 0f;

        PlayRandomOneShot(dashSFXVariations, dashSFX);
    }

    void HandleDash()
    {
        dashTimer += deltaTime;

        if (dashTimer >= dashDuration * 0.5f && isInvincible)
            isInvincible = false;

        if (MoveInput.JumpDown && canMove)
        {
            PerformDashJump();
            return;
        }

        if (dashTimer >= dashDuration)
        {
            dashMomentum = dashDirection * dashSpeed;
            hasMomentum = true;
            isDashing = false;
            isInvincible = false;
            dashEndTime = currentTime;
            return;
        }

        tempVector = dashDirection * dashSpeed * deltaTime;
        characterController.Move(tempVector);
    }

    void PerformDashJump()
    {
        if (!UseStamina(dashJumpStaminaCost)) return;

        float dashCompletion = Mathf.Clamp01(dashTimer / dashDuration);
        float totalSpeed = dashSpeed + dashSpeed * (dashJumpSpeedMultiplier - 1f) * dashCompletion;

        isDashJump = true;
        moveDirection.y = jumpPower;

        dashMomentum = dashDirection * totalSpeed;
        hasMomentum = true;

        isDashing = false;
        isInvincible = false;
        dashEndTime = currentTime;

        PlayRandomOneShot(dashJumpSFXVariations, dashJumpSFX);
    }

    // =====================================================================
    // AIR DASH
    // =====================================================================

    void CheckForAirDash()
    {
        if (isGrounded) return;
        if (currentTime - lastAirDashTime < airDashCooldown) return;
        if (currentAirDashCount >= maxAirDashes) return;

        if (MoveInput.WDown)
        {
            if (currentTime - airLastWTap < doubleTapWindow) StartAirDash(cachedTransform.forward);
            airLastWTap = currentTime;
        }
        if (MoveInput.ADown)
        {
            if (currentTime - airLastATap < doubleTapWindow) StartAirDash(-cachedTransform.right);
            airLastATap = currentTime;
        }
        if (MoveInput.SDown)
        {
            if (currentTime - airLastSTap < doubleTapWindow) StartAirDash(-cachedTransform.forward);
            airLastSTap = currentTime;
        }
        if (MoveInput.DDown)
        {
            if (currentTime - airLastDTap < doubleTapWindow) StartAirDash(cachedTransform.right);
            airLastDTap = currentTime;
        }
    }

    void StartAirDash(Vector3 direction)
    {
        if (!UseStamina(airDashStaminaCost)) return;

        currentAirDashCount++;
        lastAirDashTime = currentTime;

        isDashing = true;
        dashTimer = 0f;
        dashDirection = direction.normalized;
        dashDirection.y = Mathf.Clamp(dashDirection.y, -0.5f, 0.5f);
        dashDirection.Normalize();
        isInvincible = true;

        PlayRandomOneShot(airDashSFXVariations, airDashSFX);
    }

    // =====================================================================
    // SLIDE
    // =====================================================================

    void CheckForSlide()
    {
        if (!isGrounded) return;
        if (!MoveInput.CrouchDown) return;
        if (currentTime - lastSlideTime <= slideCooldown) return;

        bool hasSpeed = MoveInput.RunHeld || hasMomentum;
        if (hasSpeed) StartSlide();
    }

    void StartSlide()
    {
        if (!UseStamina(slideStaminaCost)) return;

        isSliding = true;
        slideTimer = 0f;
        lastSlideTime = currentTime;

        inputDir.x = horizontalInput;
        inputDir.y = 0f;
        inputDir.z = verticalInput;

        slideDirection = inputDir.magnitude < inputDeadzone
            ? cachedTransform.forward
            : cachedTransform.TransformDirection(inputDir).normalized;

        momentumHorizontal.x = moveDirection.x;
        momentumHorizontal.y = 0f;
        momentumHorizontal.z = moveDirection.z;
        float currentSpeed = momentumHorizontal.magnitude;

        if (hasMomentum)
        {
            momentumHorizontal.x = dashMomentum.x;
            momentumHorizontal.z = dashMomentum.z;
            float momentumSpeed = momentumHorizontal.magnitude;
            float bestSpeed = Mathf.Max(currentSpeed, momentumSpeed);
            slideSpeed = bestSpeed;

            momentumHorizontal.Normalize();
            slideDirection = Vector3.Lerp(slideDirection, momentumHorizontal, 0.7f);

            hasMomentum = false;
            dashMomentum = Vector3.zero;
        }
        else if (currentSpeed > slideSpeed)
        {
            slideSpeed = currentSpeed;
        }

        PlayRandomLooping(slideSFXVariations, slideSFX);
        characterController.height = crouchHeight;
    }

    void HandleSlide()
    {
        slideTimer += deltaTime;

        float speedPercent = Mathf.Clamp01(1f - (slideTimer / slideDuration));
        currentSlideSpeed = Mathf.Lerp(slideMinSpeed, slideSpeed, Mathf.Pow(speedPercent, slideDeceleration));

        inputDir.x = horizontalInput;
        inputDir.y = 0f;
        inputDir.z = verticalInput;

        if (inputDir.magnitude > inputDeadzone)
        {
            tempVector = cachedTransform.TransformDirection(inputDir).normalized;
            slideDirection = Vector3.Lerp(slideDirection, tempVector, slideControlStrength * deltaTime).normalized;
        }

        // Slide jump check
        if (MoveInput.JumpDown && canMove)
        {
            PerformSlideJump();
            return;
        }

        bool timeExpired = slideTimer >= slideDuration;
        bool manualCancel = !MoveInput.CrouchHeld && currentSlideSpeed <= slideMinSpeed * 1.5f;

        if (timeExpired || manualCancel)
        {
            EndSlide();
            return;
        }

        moveDirection.y = isGrounded ? groundedYVelocity : moveDirection.y - gravity * deltaTime;

        tempVector.x = slideDirection.x * currentSlideSpeed * deltaTime;
        tempVector.y = moveDirection.y * deltaTime;
        tempVector.z = slideDirection.z * currentSlideSpeed * deltaTime;
        characterController.Move(tempVector);
    }

    void EndSlide()
    {
        isSliding = false;
        characterController.height = defaultHeight;
        StopLooping();
    }

    void PerformSlideJump()
    {
        tempVector.x = slideDirection.x * currentSlideSpeed * slideJumpSpeedMultiplier;
        tempVector.y = 0f;
        tempVector.z = slideDirection.z * currentSlideSpeed * slideJumpSpeedMultiplier;

        dashMomentum = tempVector;
        hasMomentum = true;

        moveDirection = tempVector;
        moveDirection.y = jumpPower;

        PlayRandomOneShot(slideJumpSFXVariations, slideJumpSFX);
        EndSlide();
    }

    // =====================================================================
    // DOWNSLAM
    // =====================================================================

    void StartDownslam()
    {
        isDownslamming = true;
        moveDirection = Vector3.zero;
        hasMomentum = false;
        dashMomentum = Vector3.zero;
        PlayRandomLooping(downslamSFXVariations, downslamSFX);
    }

    void HandleDownslam()
    {
        moveDirection.y = -slamSpeed;
        characterController.Move(moveDirection * deltaTime);

        if (isGrounded)
        {
            PerformSlamImpact();
            isDownslamming = false;
            StopLooping();
        }
    }

    void PerformSlamImpact()
    {
        PlayRandomOneShot(slamImpactSFXVariations, slamImpactSFX);

        Collider[] hitColliders = Physics.OverlapSphere(cachedTransform.position, slamRadius);
        int hitCount = hitColliders.Length;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitColliders[i];
            if (hit.gameObject == gameObject) continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(slamDamage, cachedTransform.position, DamageType.Normal);

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                tempVector = (hit.transform.position - cachedTransform.position).normalized;
                rb.AddForce(tempVector * slamKnockbackForce, ForceMode.Impulse);
            }
        }
    }

    // =====================================================================
    // HIGH JUMP
    // =====================================================================

    void CheckForHighJump()
    {
        if (MoveInput.HighJumpDown && (isGrounded || isWallClinging || isWallClimbing))
            StartHighJumpCharge();

        if (MoveInput.HighJumpHeld && isChargingHighJump)
            highJumpChargeTime = Mathf.Min(highJumpChargeTime + deltaTime, maxHighJumpChargeTime);

        if (MoveInput.HighJumpUp && isChargingHighJump)
            PerformHighJump();
    }

    void StartHighJumpCharge()
    {
        if (currentStamina < highJumpStaminaCost) return;
        isChargingHighJump = true;
        highJumpChargeTime = 0f;
    }

    void PerformHighJump()
    {
        if (!isChargingHighJump) return;

        if (!UseStamina(highJumpStaminaCost))
        {
            isChargingHighJump = false;
            return;
        }

        float chargeMultiplier = 1f + (highJumpChargeTime * highJumpChargePerSecond);
        float jumpHeight = jumpPower * highJumpMultiplier * chargeMultiplier;

        moveDirection.y = jumpHeight;
        isChargingHighJump = false;
        highJumpChargeTime = 0f;

        if (isWallClinging || isWallClimbing)
        {
            tempVector = -wallNormal;
            tempVector.y = 0f;
            tempVector.Normalize();
            dashMomentum = tempVector * dashSpeed * 0.5f;
            hasMomentum = true;
            isWallClinging = false;
            isWallClimbing = false;
        }

        PlayRandomJumpSFX();
    }

    // =====================================================================
    // WALL INTERACTIONS
    // =====================================================================

    void CheckForWallInteractions()
    {
        if (isGrounded)
        {
            wasOnWall = false;
            if (isWallClinging || isWallClimbing || isWallRunning)
                EndWallInteraction();
            return;
        }

        RaycastHit wallHit;
        Vector3 rayOrigin = cachedTransform.position + Vector3.up * 0.5f;

        bool wallDetected =
            Physics.Raycast(rayOrigin, cachedTransform.forward, out wallHit, wallDetectionDistance, wallLayer) ||
            Physics.Raycast(rayOrigin, cachedTransform.right, out wallHit, wallDetectionDistance, wallLayer) ||
            Physics.Raycast(rayOrigin, -cachedTransform.right, out wallHit, wallDetectionDistance, wallLayer);

        if (wallDetected)
        {
            wallNormal = wallHit.normal;
            wallPoint = wallHit.point;
            wasOnWall = true;

            if (MoveInput.JumpDown)
            {
                if (currentTime - lastSpaceTap < doubleTapWindow)
                {
                    if (MoveInput.RunHeld) StartWallRun();
                    else StartWallCling();
                }
                lastSpaceTap = currentTime;
            }

            if (isWallClinging) HandleWallCling();
            else if (isWallClimbing) HandleWallClimb();
            else if (isWallRunning) HandleWallRun();
        }
        else if (wasOnWall && (isWallClinging || isWallClimbing || isWallRunning))
        {
            EndWallInteraction();
        }
    }

    void StartWallCling()
    {
        isWallClinging = true;
        isWallClimbing = false;
        isWallRunning = false;
        moveDirection.y = 0f;
    }

    void HandleWallCling()
    {
        moveDirection.y -= gravity * wallClingGravityReduction * deltaTime;

        if (MoveInput.JumpDown) { PerformWallHop(); return; }
        if (MoveInput.HighJumpDown) StartHighJumpCharge();

        if (MoveInput.WHeld) { isWallClimbing = true; isWallClinging = false; }
        else if (MoveInput.SHeld) EndWallInteraction();

        tempVector = wallPoint - cachedTransform.position;
        float distanceToWall = Vector3.Dot(tempVector, wallNormal);
        if (distanceToWall > 0.1f)
            characterController.Move(-wallNormal * distanceToWall * 10f * deltaTime);

        characterController.Move(moveDirection * deltaTime);
    }

    void HandleWallClimb()
    {
        moveDirection.y = 0f;

        if (MoveInput.WHeld) moveDirection.y = wallClimbSpeed;
        else if (MoveInput.SHeld) moveDirection.y = -wallClimbSpeed;

        float horizontalMove = MoveInput.AHeld ? -wallClimbSpeed : MoveInput.DHeld ? wallClimbSpeed : 0f;
        if (horizontalMove != 0f)
        {
            tempVector = Vector3.Cross(wallNormal, Vector3.up).normalized * horizontalMove;
            moveDirection += tempVector;
        }

        if (MoveInput.JumpDown) { PerformWallHop(); return; }
        if (MoveInput.HighJumpDown) StartHighJumpCharge();

        characterController.Move(moveDirection * deltaTime);
    }

    void PerformWallHop()
    {
        tempVector = -wallNormal;
        tempVector.y = 0.3f;
        tempVector.Normalize();

        moveDirection = tempVector * wallHopForce;
        moveDirection.y = jumpPower * 0.8f;

        isWallClinging = false;
        isWallClimbing = false;
        hasMomentum = true;
        dashMomentum = tempVector * wallHopForce;

        PlayRandomOneShot(wallHopSFXVariations, wallHopSFX);
    }

    void StartWallRun()
    {
        if (!MoveInput.RunHeld) return;

        float wallAngle = Vector3.Angle(-wallNormal, Vector3.up);
        if (wallAngle < wallRunMinAngle || wallAngle > wallRunMaxAngle) return;

        isWallRunning = true;
        isWallClinging = false;
        isWallClimbing = false;

        tempVector = Vector3.Cross(wallNormal, Vector3.up).normalized;
        if (Vector3.Dot(tempVector, cachedTransform.forward) < 0f) tempVector = -tempVector;

        moveDirection = tempVector * wallRunSpeed;
        moveDirection.y = 0f;
    }

    void HandleWallRun()
    {
        if (!MoveInput.RunHeld) { EndWallInteraction(); return; }

        moveDirection.y -= gravity * wallRunGravityReduction * deltaTime;

        tempVector = Vector3.Cross(wallNormal, Vector3.up).normalized;
        if (Vector3.Dot(tempVector, cachedTransform.forward) < 0f) tempVector = -tempVector;

        moveDirection.x = tempVector.x * wallRunSpeed;
        moveDirection.z = tempVector.z * wallRunSpeed;

        characterController.Move(moveDirection * deltaTime);
    }

    void EndWallInteraction()
    {
        isWallClinging = false;
        isWallClimbing = false;
        isWallRunning = false;
        wasOnWall = false;
    }

    // =====================================================================
    // GRAPPLE
    // =====================================================================

    void CheckForGrapple()
    {
        if (MoveInput.GrappleDown && !isGrappling) StartGrapple();
    }

    void StartGrapple()
    {
        RaycastHit hit;
        if (!Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, grappleRange)) return;

        int hitLayer = hit.collider.gameObject.layer;
        bool isHeavy = (heavyLayer & (1 << hitLayer)) != 0;
        bool isLight = (lightLayer & (1 << hitLayer)) != 0;

        if (!isHeavy && !isLight) return;

        isGrappling = true;
        grappleTarget = hit.point;
        grappleTargetTransform = hit.transform;
        isGrappleHeavy = isHeavy;
        grappleDistance = hit.distance;

        if (grappleLine == null)
        {
            GameObject lineObj = new GameObject("GrappleLine");
            lineObj.transform.SetParent(cachedTransform);
            grappleLine = lineObj.AddComponent<LineRenderer>();
            grappleLine.material = new Material(Shader.Find("Sprites/Default"));
            grappleLine.startColor = Color.yellow;
            grappleLine.endColor = Color.yellow;
            grappleLine.startWidth = 0.1f;
            grappleLine.endWidth = 0.05f;
            grappleLine.positionCount = 2;
        }
    }

    void HandleGrapple()
    {
        if (grappleTargetTransform != null)
            grappleTarget = grappleTargetTransform.position;

        grappleDirection = (grappleTarget - cachedTransform.position).normalized;
        float currentDistance = Vector3.Distance(cachedTransform.position, grappleTarget);

        if (isGrappleHeavy)
        {
            characterController.Move(grappleDirection * grapplePullSpeed * deltaTime);
            if (currentDistance < 1f) { EndGrapple(); return; }
        }
        else
        {
            Rigidbody targetRb = grappleTargetTransform != null ? grappleTargetTransform.GetComponent<Rigidbody>() : null;
            if (targetRb != null)
                targetRb.AddForce(-grappleDirection * grapplePullSpeed, ForceMode.Force);
            if (currentDistance < 2f) { EndGrapple(); return; }
        }

        if (grappleLine != null)
        {
            grappleLine.SetPosition(0, cachedTransform.position + Vector3.up * 0.5f);
            grappleLine.SetPosition(1, grappleTarget);
        }

        if (MoveInput.GrappleDown || isGrounded) EndGrapple();
    }

    void EndGrapple()
    {
        isGrappling = false;
        grappleTargetTransform = null;
        if (grappleLine != null)
        {
            Destroy(grappleLine.gameObject);
            grappleLine = null;
        }
    }

    // =====================================================================
    // DOUBLE JUMP
    // =====================================================================

    void CheckForDoubleJump()
    {
        if (isGrounded)
        {
            hasDoubleJumped = false;
            wasGrounded = true;
            isFalling = false;
            fallStartHeight = cachedTransform.position.y;
            return;
        }

        if (wasGrounded)
        {
            isFalling = true;
            fallStartHeight = cachedTransform.position.y;
        }
        wasGrounded = false;

        if (MoveInput.JumpDown && !hasDoubleJumped && enableDoubleJump)
        {
            if (currentTime - lastSpaceTap < doubleTapWindow)
                PerformDoubleJump();
            lastSpaceTap = currentTime;
        }
    }

    void PerformDoubleJump()
    {
        if (requiresAirMagic) return;
        if (!UseStamina(doubleJumpStaminaCost)) return;

        moveDirection.y = doubleJumpPower;
        hasDoubleJumped = true;

        PlayRandomOneShot(doubleJumpSFXVariations, doubleJumpSFX);
    }

    // =====================================================================
    // ROLL
    // =====================================================================

    void CheckForRoll()
    {
        if (currentTime - lastRollTime < rollCooldown) return;
        bool ctrlHeld = MoveInput.CrouchHeld;
        if (!ctrlHeld) return;

        if (MoveInput.WDown)
        {
            if (currentTime - rollLastWTap < doubleTapWindow) StartRoll(cachedTransform.forward);
            rollLastWTap = currentTime;
        }
        if (MoveInput.SDown)
        {
            if (currentTime - rollLastSTap < doubleTapWindow) StartRoll(-cachedTransform.forward);
            rollLastSTap = currentTime;
        }
        if (MoveInput.ADown)
        {
            if (currentTime - rollLastATap < doubleTapWindow) StartRoll(-cachedTransform.right);
            rollLastATap = currentTime;
        }
        if (MoveInput.DDown)
        {
            if (currentTime - rollLastDTap < doubleTapWindow) StartRoll(cachedTransform.right);
            rollLastDTap = currentTime;
        }
    }

    void StartRoll(Vector3 direction)
    {
        // FIX: Roll now has a consistent stamina cost like all other abilities
        if (!UseStamina(rollStaminaCost)) return;

        isRolling = true;
        rollTimer = 0f;
        rollDirection = direction.normalized;
        lastRollTime = currentTime;
        characterController.height = crouchHeight;
        PlayRandomOneShot(rollSFXVariations, rollSFX);
    }

    void HandleRoll()
    {
        rollTimer += deltaTime;

        if (rollTimer >= rollDuration || isGrounded)
        {
            EndRoll();
            return;
        }

        characterController.Move(rollDirection * rollSpeed * deltaTime);
    }

    void EndRoll()
    {
        isRolling = false;
        characterController.height = defaultHeight;
    }

    // =====================================================================
    // LEDGE GRAB
    // =====================================================================

    void CheckForLedgeGrab()
    {
        if (isGrounded || isWallClinging || isWallClimbing)
        {
            if (isLedgeGrabbing) EndLedgeGrab();
            return;
        }

        if (!isLedgeGrabbing && currentTime - lastLedgeGrabTime < ledgeGrabCooldown) return;

        Vector3 rayOrigin = cachedTransform.position + Vector3.up * 0.5f;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, cachedTransform.forward, out hit, ledgeDetectionDistance))
        {
            Vector3 ledgeTop = hit.point + Vector3.up * ledgeDetectionHeight;
            if (!Physics.CheckSphere(ledgeTop, 0.3f))
                StartLedgeGrab(hit.point, hit.normal);
        }
    }

    void StartLedgeGrab(Vector3 ledgePos, Vector3 normal)
    {
        isLedgeGrabbing = true;
        ledgePosition = ledgePos;
        ledgeNormal = normal;
        moveDirection.y = 0f;
        lastLedgeGrabTime = currentTime;
        PlayRandomOneShot(ledgeGrabSFXVariations, ledgeGrabSFX);
    }

    void HandleLedgeGrab()
    {
        moveDirection.y = 0f;

        if (MoveInput.JumpDown) { PerformLedgeClimb(); return; }
        if (MoveInput.SHeld) { EndLedgeGrab(); return; }

        characterController.Move(Vector3.zero);
    }

    void PerformLedgeClimb()
    {
        Vector3 climbTarget = ledgePosition + Vector3.up * (defaultHeight * 0.5f);
        tempVector = climbTarget - cachedTransform.position;
        tempVector.y = 0f;

        if (tempVector.magnitude > 0.1f)
            characterController.Move(tempVector.normalized * ledgeClimbSpeed * deltaTime);

        moveDirection.y = ledgeClimbSpeed;
        characterController.Move(moveDirection * deltaTime);

        if (cachedTransform.position.y >= climbTarget.y - 0.1f)
            EndLedgeGrab();
    }

    void EndLedgeGrab()
    {
        isLedgeGrabbing = false;
    }

    // =====================================================================
    // GLIDE
    // =====================================================================

    void CheckForGlide()
    {
        if (isGrounded)
        {
            if (isGliding) EndGlide();
            return;
        }

        if (MoveInput.GlideDown) StartGlide();

        if (MoveInput.GlideHeld && isGliding) HandleGlide();
        else if (isGliding) EndGlide();
    }

    void StartGlide()
    {
        isGliding = true;
        PlayRandomOneShot(glideSFXVariations, glideSFX);
    }

    void HandleGlide()
    {
        if (currentStamina <= 0f) { EndGlide(); return; }

        moveDirection.y -= gravity * glideGravityReduction * deltaTime;
        moveDirection.x *= glideSpeedMultiplier;
        moveDirection.z *= glideSpeedMultiplier;

        UseStamina(glideStaminaDrain * deltaTime);

        characterController.Move(moveDirection * deltaTime);
    }

    void EndGlide()
    {
        isGliding = false;
    }

    // =====================================================================
    // CAMERA
    // =====================================================================

    void HandleMouseLook()
    {
        if (!canMove || isDashing) return;

        rotationX += -MoveInput.MouseY * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        cachedTransform.rotation *= Quaternion.Euler(0f, MoveInput.MouseX * lookSpeed, 0f);
    }

    // =====================================================================
    // FALL DAMAGE
    // =====================================================================

    void CalculateFallDamage()
    {
        if (!isGrounded || !isFalling) return;

        float fallDistance = fallStartHeight - cachedTransform.position.y;
        if (fallDistance <= 0f) { isFalling = false; return; }

        float damageReduction = rollFallDamageReduction;
        if (fallDistance > rollFallDamageBaseHeight)
        {
            float extraHeight = fallDistance - rollFallDamageBaseHeight;
            int extra12MBlocks = Mathf.FloorToInt(extraHeight / 12f);
            damageReduction = Mathf.Max(0f, damageReduction - extra12MBlocks * rollFallDamageReductionPer12M);
        }

        isFalling = false;
    }

    void UpdateFallTracking()
    {
        if (isGrounded)
        {
            lastGroundHeight = cachedTransform.position.y;

            if (enableFallDamage && totalFallDistance > fallDamageThreshold)
            {
                float damage = (totalFallDistance - fallDamageThreshold) * fallDamagePerUnit;
                PlayRandomOneShot(fallDamageClipVariations, fallDamageClip);
                if (enableLandingEffects) ApplyLandingShake();
            }

            totalFallDistance = 0f;
        }
        else
        {
            float currentHeight = cachedTransform.position.y;
            if (currentHeight < lastGroundHeight)
                totalFallDistance = lastGroundHeight - currentHeight;
        }
    }

    // =====================================================================
    // FOOTSTEPS
    // =====================================================================

    void UpdateFootsteps()
    {
        if (!isGrounded || (verticalInput == 0f && horizontalInput == 0f))
        {
            nextFootstepTime = currentTime;
            return;
        }

        if (currentTime >= nextFootstepTime)
        {
            float interval = (isSprinting || MoveInput.RunHeld)
                ? footstepIntervalWhileRunning
                : footstepInterval;

            PlayRandomOneShot(footstepSFXVariations, footstepSFX, 0.5f);

            nextFootstepTime = currentTime + interval;
        }
    }

    // =====================================================================
    // EXHAUSTION
    // =====================================================================

    void UpdateExhaustion()
    {
        if (!enableExhaustion) return;

        if (!isExhausted && currentStamina / maxStamina < exhaustionThreshold)
        {
            isExhausted = true;
            exhaustionEndTime = currentTime + exhaustionRecoveryTime;
        }

        if (isExhausted && currentTime > exhaustionEndTime)
            isExhausted = false;
    }

    // =====================================================================
    // MOVEMENT STATE MACHINE
    // =====================================================================

    void UpdateMovementState()
    {
        if (isDashing)           currentMovementState = MovementState.Dashing;
        else if (isSliding)      currentMovementState = MovementState.Sliding;
        else if (isWallRunning)  currentMovementState = MovementState.WallRunning;
        else if (!isGrounded)    currentMovementState = MovementState.Falling;
        else if (isCrouching)    currentMovementState = MovementState.Crouching;
        else if (isSprinting)    currentMovementState = MovementState.Sprinting;
        else if (MoveInput.RunHeld && currentStamina > 0 && (verticalInput != 0 || horizontalInput != 0))
                                 currentMovementState = MovementState.Running;
        else if (verticalInput != 0 || horizontalInput != 0)
                                 currentMovementState = MovementState.Walking;
        else                     currentMovementState = MovementState.Idle;
    }

    // =====================================================================
    // CAMERA SHAKE
    // =====================================================================

    void ApplyLandingShake()
    {
        if (!enableLandingEffects || playerCamera == null) return;
        StartCoroutine(CameraShakeCoroutine(landingCameraShakeMagnitude, landingCameraShakeDuration));
    }

    private IEnumerator CameraShakeCoroutine(float magnitude, float duration)
    {
        Vector3 originalPos = playerCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            playerCamera.transform.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPos;
    }

    // =====================================================================
    // AUDIO HELPERS
    // =====================================================================

    void PlayOneShot(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    AudioClip PickRandom(AudioClip[] variations, AudioClip fallback)
    {
        if (variations != null && variations.Length > 0)
        {
            AudioClip c = variations[Random.Range(0, variations.Length)];
            if (c != null) return c;
        }
        return fallback;
    }

    void PlayRandomOneShot(AudioClip[] variations, AudioClip fallback, float volume = 1f)
    {
        AudioClip clip = PickRandom(variations, fallback);
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, volume);
    }

    void PlayRandomJumpSFX()
    {
        PlayRandomOneShot(jumpSFXVariations, jumpSFX);
    }

    void PlayRandomLooping(AudioClip[] variations, AudioClip fallback)
    {
        AudioClip clip = PickRandom(variations, fallback);
        if (clip != null && loopingAudioSource != null)
        {
            loopingAudioSource.clip = clip;
            loopingAudioSource.Play();
        }
    }

    void PlayLooping(AudioClip clip)
    {
        if (clip != null && loopingAudioSource != null)
        {
            loopingAudioSource.clip = clip;
            loopingAudioSource.Play();
        }
    }

    void StopLooping()
    {
        if (loopingAudioSource != null)
            loopingAudioSource.Stop();
    }

    // =====================================================================
    // STAMINA HELPER
    // Centralised stamina consumption — returns false if not enough stamina
    // =====================================================================

    bool UseStamina(float amount)
    {
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        lastStaminaUse = currentTime;
        return true;
    }

    // =====================================================================
    // ADVANCED MOMENTUM API
    // =====================================================================

    public void AddMomentum(Vector3 direction, float speed, string sourceType = "dash")
    {
        if (!enableAdvancedMomentum) return;

        direction.Normalize();
        Vector3 newMomentum = direction * Mathf.Min(speed, momentumMaxSpeed);

        switch (sourceType.ToLower())
        {
            case "dash":    dashMomentum    = newMomentum; break;
            case "slide":   slideMomentum   = newMomentum; break;
            case "jump":    jumpMomentum    = newMomentum; break;
            case "wallrun": wallRunMomentum = newMomentum; break;
        }

        activeMomentumSources++;
        hasMomentum = true;
        momentumDuration = 2f;
        previousMomentumDirection = direction;
    }

    public void PreserveMomentumThroughAction()
    {
        if (!enableEnergyConservation) return;
        dashMomentum    *= energyConservationLoss;
        slideMomentum   *= energyConservationLoss;
        jumpMomentum    *= energyConservationLoss;
        wallRunMomentum *= energyConservationLoss;
    }

    public void ClearMomentum(string sourceType = "all")
    {
        switch (sourceType.ToLower())
        {
            case "dash":    dashMomentum    = Vector3.zero; break;
            case "slide":   slideMomentum   = Vector3.zero; break;
            case "jump":    jumpMomentum    = Vector3.zero; break;
            case "wallrun": wallRunMomentum = Vector3.zero; break;
            case "all":
                dashMomentum = slideMomentum = jumpMomentum = wallRunMomentum = Vector3.zero;
                activeMomentumSources = 0;
                hasMomentum = false;
                break;
        }
    }

    private Vector3 GetCombinedMomentum()
    {
        if (!enableMomentumStacking) return dashMomentum;
        combinedMomentum = dashMomentum + slideMomentum + jumpMomentum + wallRunMomentum;
        return Vector3.ClampMagnitude(combinedMomentum, momentumMaxSpeed);
    }

    public float GetMomentumSpeed()    => GetCombinedMomentum().magnitude;
    public bool HasActiveMomentum()    => GetMomentumSpeed() >= minMomentumSpeed;

    // =====================================================================
    // PUBLIC API
    // =====================================================================

    public void RestoreStamina(float amount)      => currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
    public void RestoreFullStamina()              => currentStamina = maxStamina;
    public bool IsInvincible()                    => isInvincible;
    public float GetCurrentStamina()              => currentStamina;
    public float GetMaxStamina()                  => maxStamina;
    public MovementState GetCurrentMovementState()=> currentMovementState;
    public bool IsInSafeZone()                    => isInSafeZone;
    public void SetSafeZoneStatus(bool inZone)    => isInSafeZone = inZone;

    public void TakeKnockback(Vector3 direction, float force)
    {
        if (!enableKnockback) return;
        knockbackVelocity = direction.normalized * force * knockbackResistance;
        knockbackEndTime = currentTime + 0.3f;
    }

    public void StartSprint()
    {
        if (!enableSprinting || isSprinting || currentTime - lastSprintTime < sprintCooldown) return;
        isSprinting = true;
    }

    public void StopSprint()
    {
        if (!isSprinting) return;
        isSprinting = false;
        lastSprintTime = currentTime;
    }

    public void StartCrouch()
    {
        if (!enableCrouch || isCrouching) return;
        isCrouching = true;
        characterController.height = crouchColliderHeight;
    }

    public void StopCrouch()
    {
        if (!isCrouching) return;
        isCrouching = false;
        characterController.height = defaultHeight;
    }

    public bool IsGrounded => isGrounded;

    public float GetCurrentVelocity()
    {
        if (!enableVelocityTracking) return 0f;
        currentVelocity = moveDirection.magnitude;
        maxVelocityReached = Mathf.Max(maxVelocityReached, currentVelocity);
        return currentVelocity;
    }

    public float GetMaxVelocityReached() => maxVelocityReached;
    public void ResetMaxVelocity()       => maxVelocityReached = 0f;

    // Input buffer
    public void BufferInput(string commandName)
    {
        inputBuffer.Enqueue(commandName);
        if (inputBuffer.Count > 10) inputBuffer.Dequeue();
    }
    public string GetBufferedInput() => inputBuffer.Count > 0 ? inputBuffer.Dequeue() : null;

    // Cooldown system
    public void StartCooldown(string abilityName, float duration) => abilityCooldowns[abilityName] = currentTime + duration;
    public float GetCooldownRemaining(string abilityName) => abilityCooldowns.TryGetValue(abilityName, out float t) ? Mathf.Max(0f, t - currentTime) : 0f;
    public bool IsAbilityAvailable(string abilityName) => GetCooldownRemaining(abilityName) <= 0f;

    // =====================================================================
    // GIZMOS
    // =====================================================================

    // =====================================================================
    // PORTAL INTEGRATION
    // =====================================================================

    /// <summary>
    /// Called by PortalTraverser after a teleport. The portal system moves the
    /// Transform/Rigidbody; this rotates every velocity vector the controller keeps
    /// privately so dashes, slides, wall runs and momentum chains continue naturally.
    /// </summary>
    public void ApplyPortalRotation(Quaternion rotation)
    {
        moveDirection = rotation * moveDirection;
        dashMomentum = rotation * dashMomentum;
        slideMomentum = rotation * slideMomentum;
        jumpMomentum = rotation * jumpMomentum;
        wallRunMomentum = rotation * wallRunMomentum;
        combinedMomentum = rotation * combinedMomentum;
        knockbackVelocity = rotation * knockbackVelocity;

        dashDirection = rotation * dashDirection;
        slideDirection = rotation * slideDirection;
        airBoostDirection = rotation * airBoostDirection;
        rollDirection = rotation * rollDirection;
        grappleDirection = rotation * grappleDirection;

        // Pitch is stored separately on the child camera; reset it so a traversal
        // through a rotated/ceiling portal cannot leave the view rolled or inverted.
        rotationX = 0f;
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.identity;
    }

    void OnDrawGizmosSelected()
    {
        Transform gizmoTransform = cachedTransform != null ? cachedTransform : transform;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(gizmoTransform.position, slamRadius);

        if (hasMomentum && Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(gizmoTransform.position, dashMomentum.normalized * 3f);

            if (enableAdvancedMomentum && enableMomentumStacking)
            {
                Vector3 combined = GetCombinedMomentum();
                if (combined.magnitude > minMomentumSpeed)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(gizmoTransform.position + Vector3.up, combined.normalized * 3f);
                }
            }
        }
    }
}