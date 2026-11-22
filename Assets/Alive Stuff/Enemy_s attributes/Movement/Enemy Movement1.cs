using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class EnemyMovement : MonoBehaviour
{
    #region Enums
    public enum AIState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Retreat,
        Flanking,
        Search,
        TakeCover,
        IsIt,
        Tagged
    }

    public enum PredictionMethod
    {
        Linear,
        Quadratic,
        Adaptive
    }
    #endregion

    #region Inspector Fields
    [Header("Movement Driver")]
    [Tooltip("When true, PlayerMovement (AI controlled) drives the CharacterController; this AI only decides where to go.")]
    public bool usePlayerMovement = false;

    [Header("Target")]
    public Transform player;
    public float detectRange = 60f;
    public float loseRange = 80f;
    public float attackRange = 3f;
    public float retreatHealthThreshold = 0.3f;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float chaseSpeedMultiplier = 1.2f;
    public float retreatSpeedMultiplier = 1.5f;
    public float gravity = 20f;
    public float rotationSpeed = 10f;
    public float minMoveDistance = 1.5f;

    [Header("Dash")]
    public float dashSpeed = 28f;
    public float dashTime = 0.2f;
    public float dashCooldown = 1.2f;
    public float dashMinDistance = 6f;
    public float dashMaxDistance = 18f;
    public float dashOptimalDistance = 12f;

    [Header("AI Behavior")]
    public PredictionMethod predictionMethod = PredictionMethod.Adaptive;
    public float predictionTime = 0.6f;
    public float predictionAdaptationRate = 0.1f;
    public bool enableFlanking = true;
    public float flankingAngle = 45f;
    public float flankingDistance = 8f;
    public float obstacleAvoidanceRadius = 2f;
    public float obstacleAvoidanceForce = 5f;
    [Tooltip("Use a dedicated 'Obstacle' or 'Environment' layer so invisible/trigger colliders don't block. -1 = Everything (can cause stuck on invisible colliders).")]
    public LayerMask obstacleLayer = -1;
    public bool useLineOfSight = true;
    public float lineOfSightUpdateInterval = 0.2f;

    [Header("Advanced State System")]
    public bool useStatePriorities = true;
    public bool useStateCooldowns = true;
    public bool useStateEvaluation = true;
    public float stateEvaluationInterval = 0.3f;
    public float minStateDuration = 0.5f;
    public float stateHistoryMemory = 10f;
    public float stateTransitionHysteresis = 0.2f;

    [Header("Advanced Sensory System")]
    public bool useVisionCone = true;
    public float visionAngle = 120f;
    public float visionRange = 50f;
    public float peripheralVisionAngle = 180f;
    public float peripheralVisionRange = 30f;
    public bool useHearing = true;
    public float hearingRange = 40f;
    public float suspicionDecayRate = 0.5f;
    public float suspicionThreshold = 0.7f;
    public float maxSuspicion = 1f;

    [Header("Memory & Tracking")]
    public bool useMemorySystem = true;
    public float lastKnownPositionMemory = 15f;
    public float searchRadius = 25f;
    public int maxSearchPoints = 5;
    public float searchPointWaitTime = 2f;

    [Header("Cover System")]
    public bool useCoverSystem = true;
    public float coverDetectionRange = 15f;
    public float coverMinHeight = 1f;
    public float coverMaxHeight = 3f;
    public float coverEvaluationInterval = 1f;
    public LayerMask coverLayer = -1;
    public float peekFromCoverDistance = 2f;

    [Header("Threat Assessment")]
    public bool useThreatAssessment = true;
    public float threatEvaluationInterval = 0.5f;
    public float maxThreatDistance = 100f;
    public bool trackMultipleTargets = false;
    public int maxTrackedTargets = 3;

    [Header("Tactical Positioning")]
    public bool useTacticalPositioning = true;
    public float preferredCombatDistance = 8f;
    public float minCombatDistance = 3f;
    public float maxCombatDistance = 15f;
    public bool preferHeightAdvantage = true;
    public float heightAdvantageWeight = 1.5f;
    public bool avoidChokepoints = false;

    [Header("Patrol")]
    public bool enablePatrol = true;
    public float patrolRadius = 20f;
    public float patrolWaitTime = 2f;
    public int patrolWaypointCount = 4;
    public List<Vector3> customPatrolWaypoints = new List<Vector3>();
    public bool useCustomWaypoints = false;
    public bool autoGenerateWaypoints = false;
    public float waypointGizmoSize = 0.7f;

    [Header("Random Waypoint Generation")]
    public int minRandomWaypoints = 6;
    public int maxRandomWaypoints = 21;
    public float randomWaypointSpreadRadius = 30f;
    public bool generateRandomWaypoints = false;

    [Header("Tag Game - Stamina System")]
    public bool enableStaminaSystem = true;
    public float maxStamina = 100f;
    public float staminaDepletionRate = 15f;
    public float dashStaminaCost = 30f;
    public float staminaRecoveryRate = 20f;
    public float minStaminaForDash = 40f;
    public float staminaTiredThreshold = 0.3f;

    [Header("Tag Game - Tag System")]
    public bool isCurrentlyIt = false;
    public float tagSpeedBoost = 1.3f;
    public float tagBoostDuration = 2f;
    public float tagCooldown = 0.5f;
    public List<Transform> allPlayers = new List<Transform>();
    public AudioClip taggedClip;
    public AudioClip nowItClip;

    [Header("Tag Game - Safe Zones")]
    public bool useSafeZones = true;
    public LayerMask safeZoneLayer;
    public float safeZoneDetectionRange = 50f;
    public float safeZoneAvoidanceForce = 10f;

    [Header("Tag Game - Fatigue System")]
    public bool enableFatigueSystem = true;
    public float fatigueAccumulationRate = 0.3f;
    public float maxFatigue = 100f;
    public float fatigueRecoveryRate = 10f;
    public float fatigueSpeedPenalty = 0.7f;
    public float fatigueThreshold = 0.6f;

    [Header("Tag Game - Cooperative Chasing")]
    public bool enableCooperativeChasing = true;
    public float cooperativeFlankDistance = 15f;
    public float otherEnemyDetectionRange = 40f;
    public float cooperativeBonusAggressiveness = 1.2f;

    [Header("Tag Game - Mode")]
    public bool useTagGameMode = true;
    public bool enableCoverInTagMode = false;
    public bool reduceFlankingComplexity = true;

    [Header("Obstacle Escape - Jump System")]
    public bool enableObstacleEscape = true;
    public float jumpHeight = 8f;
    public float maxStuckTime = 1.5f;
    public float stuckDetectionThreshold = 0.3f;
    public float stuckCheckRate = 0.2f;
    public float jumpCooldown = 0.5f;
    public bool useMultipleJumpAttempts = true;
    public int maxJumpAttempts = 3;
    public float jumpRotationSpeed = 15f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip footstepClip;
    public AudioClip dashClip;
    public AudioClip landClip;
    public AudioClip idleClip;
    public AudioClip alertClip;
    [Range(0f, 1f)] public float footstepVolume = 0.5f;
    [Range(0f, 1f)] public float dashVolume = 1f;
    [Range(0f, 1f)] public float landVolume = 0.6f;
    [Range(0f, 1f)] public float idleVolume = 0.3f;
    [Range(0f, 1f)] public float alertVolume = 0.8f;
    #endregion

    #region Private Fields
    private CharacterController cc;
    private Transform tf;
    private EnemyHealth enemyHealth;

    private Vector3 velocity;
    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;
    private Vector3 playerAcceleration;
    private Vector3 smoothedPlayerVelocity;

    private AIState currentState = AIState.Idle;
    private AIState previousState = AIState.Idle;
    private AIState queuedState = AIState.Idle;
    private float stateTimer = 0f;
    private float stateCooldownTimer = 0f;
    private float lastStateEvaluation = 0f;
    private bool stateTransitionPending = false;

    private Dictionary<AIState, int> statePriorities = new Dictionary<AIState, int>();
    private Dictionary<AIState, float> stateCooldowns = new Dictionary<AIState, float>();
    private Dictionary<AIState, float> lastStateExitTime = new Dictionary<AIState, float>();
    private List<StateTransitionRecord> stateHistory = new List<StateTransitionRecord>();
    private const int MAX_STATE_HISTORY = 50;
    private Dictionary<AIState, float> stateScores = new Dictionary<AIState, float>();
    private Dictionary<AIState, StateParameters> stateParameters = new Dictionary<AIState, StateParameters>();
    private Dictionary<AIState, List<StateTransitionCondition>> transitionConditions = new Dictionary<AIState, List<StateTransitionCondition>>();

    private bool hasTarget;
    private bool hasLineOfSight;
    private float lastLineOfSightCheck;
    private float detectRangeSqr;
    private float loseRangeSqr;
    private float attackRangeSqr;

    private float suspicionLevel = 0f;
    private float lastSuspicionUpdate = 0f;
    private Vector3 lastKnownPlayerPosition = Vector3.zero;
    private float lastKnownPositionTime = -999f;
    private bool isSearching = false;
    private List<Vector3> searchPoints = new List<Vector3>();
    private int currentSearchPointIndex = 0;
    private float searchPointWaitTimer = 0f;

    private float visionAngleRad;
    private float peripheralVisionAngleRad;
    private float visionRangeSqr;
    private float peripheralVisionRangeSqr;

    private List<SoundEvent> recentSounds = new List<SoundEvent>();
    private const int MAX_SOUND_MEMORY = 20;

    private CoverInfo currentCover = null;
    private List<CoverInfo> availableCovers = new List<CoverInfo>();
    private float lastCoverEvaluation = 0f;
    private bool isInCover = false;
    private Vector3 coverPosition = Vector3.zero;
    private float coverPeekTimer = 0f;

    private Dictionary<Transform, ThreatInfo> trackedThreats = new Dictionary<Transform, ThreatInfo>();
    private Transform primaryThreat = null;
    private float lastThreatEvaluation = 0f;
    private float currentThreatLevel = 0f;

    private Vector3 tacticalTargetPosition = Vector3.zero;
    private bool hasTacticalPosition = false;
    private float tacticalPositionEvaluation = 0f;

    private bool isDashing;
    private float dashTimer;
    private float lastDash;

    private Queue<Vector3> playerPositionHistory = new Queue<Vector3>();
    private Queue<float> playerTimeHistory = new Queue<float>();
    private const int HISTORY_SIZE = 10;
    private float adaptivePredictionTime;
    private float predictionError;
    private float lastPredictionError;

    private Vector3 flankingTarget;
    private bool isFlanking;
    private float flankingTimer;

    private List<Vector3> patrolWaypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private float patrolWaitTimer = 0f;
    private Vector3 patrolStartPosition;
    private int selectedWaypointIndex = -1;
    
    public List<Vector3> PatrolWaypoints => patrolWaypoints;

    private Vector3 avoidanceDirection;
    private Collider[] obstacleBuffer = new Collider[8];

    private float nextFootstep;
    private float footstepRate = 0.4f;
    private bool wasGrounded;
    private bool wasChasing;

    private float currentStamina;
    private float currentFatigue = 0f;
    private float tagBoostTimer = 0f;
    private float lastTagTime = -999f;
    private bool wasTaggedThisFrame = false;
    private List<EnemyMovement> nearbyEnemies = new List<EnemyMovement>();
    private float nextCooperativeCheck = 0f;
    private bool inSafeZone = false;
    private Collider safeZoneCollider = null;

    private Vector3 lastPosition;
    private float lastDesiredHorizontalSpeed = 0f;
    private float stuckTimer = 0f;
    private bool isJumping = false;
    private bool canJump = false;
    private float jumpVelocity = 0f;
    private float stuckThreshold = 0.5f;
    private float stuckCheckInterval = 0.3f;
    private float stuckTimeLimit = 1.5f;
    private float nextStuckCheck = 0f;

    private Vector3 lastOtherTargetPosition;
    private float lastOtherTargetTime = -1f;

    private float deltaTime;
    private float currentTime;
    private Vector3 tempVector = Vector3.zero;
    private Vector3 tempVector2 = Vector3.zero;
    private float tempFloat;
    private bool tempBool;
    #endregion

    #region Advanced System Data Structures
    [System.Serializable]
    public class SoundEvent
    {
        public Vector3 position;
        public float volume;
        public float timestamp;
        public string sourceType;

        public SoundEvent(Vector3 pos, float vol, float time, string type)
        {
            position = pos;
            volume = vol;
            timestamp = time;
            sourceType = type;
        }
    }

    [System.Serializable]
    public class CoverInfo
    {
        public Vector3 position;
        public Vector3 direction;
        public float quality;
        public float distance;
        public Collider coverCollider;
        public bool isOccupied;

        public CoverInfo(Vector3 pos, Vector3 dir, float qual, float dist, Collider col)
        {
            position = pos;
            direction = dir;
            quality = qual;
            distance = dist;
            coverCollider = col;
            isOccupied = false;
        }
    }

    [System.Serializable]
    public class ThreatInfo
    {
        public Transform threat;
        public float threatLevel;
        public float distance;
        public Vector3 lastKnownPosition;
        public float lastSeenTime;
        public bool hasLineOfSight;
        public float healthPercentage;

        public ThreatInfo(Transform t, float level, float dist, Vector3 pos, float time, bool los, float health)
        {
            threat = t;
            threatLevel = level;
            distance = dist;
            lastKnownPosition = pos;
            lastSeenTime = time;
            hasLineOfSight = los;
            healthPercentage = health;
        }
    }
    #endregion

    #region State System Data Structures
    [System.Serializable]
    public class StateTransitionRecord
    {
        public AIState fromState;
        public AIState toState;
        public float timestamp;
        public float duration;
        public string reason;

        public StateTransitionRecord(AIState from, AIState to, float time, float dur, string r)
        {
            fromState = from;
            toState = to;
            timestamp = time;
            duration = dur;
            reason = r;
        }
    }

    [System.Serializable]
    public class StateParameters
    {
        public float speedMultiplier = 1f;
        public float aggressionLevel = 0.5f;
        public float cautionLevel = 0.5f;
        public float urgency = 0.5f;
        public Vector3 targetOffset = Vector3.zero;
        public float customValue1 = 0f;
        public float customValue2 = 0f;
    }

    [System.Serializable]
    public class StateTransitionCondition
    {
        public System.Func<bool> condition;
        public float weight;
        public string description;
        public bool requiresMinimumTime = false;
        public float minimumTime = 0f;

        public StateTransitionCondition(System.Func<bool> cond, float w, string desc, bool reqMin = false, float minTime = 0f)
        {
            condition = cond;
            weight = w;
            description = desc;
            requiresMinimumTime = reqMin;
            minimumTime = minTime;
        }
    }
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        Initialize();
    }

    void Update()
    {
        deltaTime = Time.deltaTime;
        currentTime = Time.time;

        UpdatePlayerTracking();
        UpdateAdvancedSensorySystem();
        UpdateThreatAssessment();
        UpdateCoverSystem();
        UpdateTacticalPositioning();
        
        if (useTagGameMode)
        {
            UpdateStaminaSystem();
            UpdateFatigueSystem();
            UpdateSafeZoneDetection();
            UpdateTagBoostTimer();
            if (enableCooperativeChasing)
                UpdateCooperativeChasing();
        }

        if (enableObstacleEscape)
        {
            UpdateStuckDetection();
        }
        
        UpdateAIState();
        ExecuteCurrentState();
        HandleAudio();
    }

    void OnDrawGizmosSelected()
    {
        if (tf == null) tf = transform;

        if (!Application.isPlaying)
        {
            if (enablePatrol && useCustomWaypoints && customPatrolWaypoints.Count > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < customPatrolWaypoints.Count; i++)
                {
                    Vector3 waypoint = customPatrolWaypoints[i];
                    Gizmos.DrawWireCube(waypoint, Vector3.one * waypointGizmoSize);
                    
                    if (i < customPatrolWaypoints.Count - 1)
                        Gizmos.DrawLine(waypoint, customPatrolWaypoints[i + 1]);
                    else
                        Gizmos.DrawLine(waypoint, customPatrolWaypoints[0]);
                }
            }
            return;
        }

        Gizmos.color = hasTarget ? new Color(1f, 0.5f, 0f, 0.8f) : new Color(1f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(tf.position, detectRange);
        
        Vector3 detectLabel = tf.position + Vector3.right * detectRange;
        Debug.DrawLine(detectLabel - Vector3.up * 0.5f, detectLabel + Vector3.up * 0.5f, Gizmos.color);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(tf.position, loseRange);
        
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Vector3 loseLabel = tf.position + Vector3.forward * loseRange;
        Debug.DrawLine(loseLabel - Vector3.up * 0.5f, loseLabel + Vector3.up * 0.5f, Gizmos.color);

        Gizmos.color = new Color(1f, 0f, 1f, 0.7f);
        Gizmos.DrawWireSphere(tf.position, attackRange);
        
        Vector3 attackLabel = tf.position + Vector3.back * attackRange;
        Debug.DrawLine(attackLabel - Vector3.right * 0.5f, attackLabel + Vector3.right * 0.5f, Gizmos.color);

        if (useVisionCone)
        {
            Gizmos.color = hasLineOfSight ? new Color(0f, 1f, 0f, 0.8f) : new Color(1f, 1f, 0f, 0.3f);
            DrawVisionCone(tf.position, tf.forward, visionAngle, visionRange);
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            DrawVisionCone(tf.position, tf.forward, peripheralVisionAngle, peripheralVisionRange);
        }

        if (useHearing)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(tf.position, hearingRange);
        }

        if (useSafeZones)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(tf.position, safeZoneDetectionRange);
        }

        if (useCoverSystem)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(tf.position, coverDetectionRange);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(tf.position, obstacleAvoidanceRadius);

        if (enableCooperativeChasing)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(tf.position, otherEnemyDetectionRange);
        }

        if (useThreatAssessment)
        {
            Gizmos.color = new Color(1f, 0f, 0.5f, 0.15f);
            Gizmos.DrawWireSphere(tf.position, maxThreatDistance);
        }

        if (hasTarget && player != null)
        {
            Gizmos.color = hasLineOfSight ? Color.green : Color.red;
            Gizmos.DrawLine(tf.position + Vector3.up, player.position + Vector3.up);
            
            Vector3 predicted = PredictPlayerPosition();
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Gizmos.DrawLine(tf.position, predicted);
            Gizmos.DrawWireSphere(predicted, 0.8f);
        }

        if (enablePatrol && patrolWaypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolWaypoints.Count; i++)
            {
                Vector3 waypoint = patrolWaypoints[i];
                
                Gizmos.DrawWireCube(waypoint, Vector3.one * waypointGizmoSize);
                
                if (i < patrolWaypoints.Count - 1)
                    Gizmos.DrawLine(waypoint, patrolWaypoints[i + 1]);
                else
                    Gizmos.DrawLine(waypoint, patrolWaypoints[0]);
                
                if (i == currentWaypointIndex)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(waypoint, waypointGizmoSize * 1.5f);
                    Gizmos.color = Color.cyan;
                }
                
                Vector3 labelPos = waypoint + Vector3.up * 2f;
                Debug.DrawLine(labelPos - Vector3.right * 0.3f, labelPos + Vector3.right * 0.3f, Gizmos.color);
            }
        }

        if (enablePatrol && useCustomWaypoints && customPatrolWaypoints.Count > 0)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 1f);
            for (int i = 0; i < customPatrolWaypoints.Count; i++)
            {
                Vector3 waypoint = customPatrolWaypoints[i];
                Gizmos.DrawWireCube(waypoint, Vector3.one * waypointGizmoSize * 1.2f);
            }
        }

        if (isFlanking)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(tf.position, flankingTarget);
            Gizmos.DrawWireSphere(flankingTarget, 1f);
        }

        if (useMemorySystem && lastKnownPositionTime > 0 && currentTime - lastKnownPositionTime < lastKnownPositionMemory)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 2f);
            Gizmos.DrawLine(tf.position, lastKnownPlayerPosition);
        }

        if (isSearching && searchPoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < searchPoints.Count; i++)
            {
                Gizmos.DrawWireSphere(searchPoints[i], 1.5f);
                if (i == currentSearchPointIndex)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(searchPoints[i], 2f);
                    Gizmos.color = Color.cyan;
                }
            }
        }

        if (useCoverSystem && currentCover != null)
        {
            Gizmos.color = isInCover ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(currentCover.position, 1f);
            Gizmos.DrawLine(tf.position, currentCover.position);
        }

        if (useTacticalPositioning && hasTacticalPosition)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(tacticalTargetPosition, 1.5f);
            Gizmos.DrawLine(tf.position, tacticalTargetPosition);
        }

        if (enableObstacleEscape)
        {
            if (isJumping)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(tf.position, 1f);
            }
            else if (stuckTimer > 0.5f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(tf.position, 0.5f);
            }
        }
    }

    void DrawVisionCone(Vector3 position, Vector3 forward, float angle, float range)
    {
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        
        Vector3 leftEdge = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
        Vector3 rightEdge = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward;
        
        Gizmos.DrawRay(position + Vector3.up, leftEdge * range);
        Gizmos.DrawRay(position + Vector3.up, rightEdge * range);
        
        int segments = 20;
        Vector3 prevPoint = position + Vector3.up + leftEdge * range;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentAngle = -halfAngle + (halfAngle * 2f * t);
            Vector3 dir = Quaternion.AngleAxis(currentAngle * Mathf.Rad2Deg, Vector3.up) * forward;
            Vector3 currentPoint = position + Vector3.up + dir * range;
            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
    }
    #endregion

    #region Initialization
    void Initialize()
    {
        cc = GetComponent<CharacterController>();
        tf = transform;
        enemyHealth = GetComponent<EnemyHealth>();

        if (!audioSource) audioSource = GetComponent<AudioSource>();

        detectRangeSqr = detectRange * detectRange;
        loseRangeSqr = loseRange * loseRange;
        attackRangeSqr = attackRange * attackRange;

        if (!player)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (!player)
        {
            enabled = false;
            return;
        }

        lastPlayerPos = player.position;
        smoothedPlayerVelocity = Vector3.zero;
        adaptivePredictionTime = predictionTime;
        patrolStartPosition = tf.position;

        if (enablePatrol)
        {
            patrolWaypoints.Clear();
            
            if (customPatrolWaypoints != null && customPatrolWaypoints.Count > 0)
            {
                patrolWaypoints = new List<Vector3>(customPatrolWaypoints);
                useCustomWaypoints = true;
            }
            else if (useCustomWaypoints && customPatrolWaypoints.Count == 0)
            {
                GeneratePatrolWaypoints();
            }
            else
            {
                GeneratePatrolWaypoints();
            }
        }

        if (autoGenerateWaypoints)
        {
            patrolWaypoints.Clear();
            GeneratePatrolWaypoints();
            customPatrolWaypoints = new List<Vector3>(patrolWaypoints);
            useCustomWaypoints = true;
            autoGenerateWaypoints = false;
        }

        if (generateRandomWaypoints)
        {
            GenerateRandomWaypoints();
        }

        if (useTagGameMode)
        {
            currentStamina = maxStamina;
            currentFatigue = 0f;
            tagBoostTimer = 0f;
        }

        if (enemyHealth != null)
        {
            enemyHealth.onHealthChanged.AddListener(OnHealthChanged);
        }

        InitializeAdvancedStateSystem();
        InitializeAdvancedSensorySystem();
    }

    void InitializeAdvancedSensorySystem()
    {
        visionAngleRad = visionAngle * Mathf.Deg2Rad;
        peripheralVisionAngleRad = peripheralVisionAngle * Mathf.Deg2Rad;
        visionRangeSqr = visionRange * visionRange;
        peripheralVisionRangeSqr = peripheralVisionRange * peripheralVisionRange;

        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
        }
    }

    void InitializeAdvancedStateSystem()
    {
        statePriorities[AIState.Retreat] = 100;
        statePriorities[AIState.Attack] = 80;
        statePriorities[AIState.TakeCover] = 75;
        statePriorities[AIState.IsIt] = 85;
        statePriorities[AIState.Tagged] = 95;
        statePriorities[AIState.Chase] = 60;
        statePriorities[AIState.Flanking] = 50;
        statePriorities[AIState.Search] = 40;
        statePriorities[AIState.Patrol] = 30;
        statePriorities[AIState.Idle] = 10;

        stateCooldowns[AIState.Retreat] = 2f;
        stateCooldowns[AIState.Flanking] = 3f;
        stateCooldowns[AIState.TakeCover] = 2f;
        stateCooldowns[AIState.Attack] = 0.5f;
        stateCooldowns[AIState.Chase] = 0.2f;
        stateCooldowns[AIState.IsIt] = 0.1f;
        stateCooldowns[AIState.Tagged] = 1f;
        stateCooldowns[AIState.Search] = 1f;
        stateCooldowns[AIState.Patrol] = 1f;
        stateCooldowns[AIState.Idle] = 0.5f;

        foreach (AIState state in System.Enum.GetValues(typeof(AIState)))
        {
            lastStateExitTime[state] = -999f;
            stateScores[state] = 0f;
            stateParameters[state] = new StateParameters();
        }

        stateParameters[AIState.Chase].speedMultiplier = chaseSpeedMultiplier;
        stateParameters[AIState.Chase].aggressionLevel = 0.8f;
        stateParameters[AIState.Chase].urgency = 0.9f;

        stateParameters[AIState.Attack].speedMultiplier = 0.7f;
        stateParameters[AIState.Attack].aggressionLevel = 1.0f;
        stateParameters[AIState.Attack].cautionLevel = 0.3f;

        stateParameters[AIState.Retreat].speedMultiplier = retreatSpeedMultiplier;
        stateParameters[AIState.Retreat].aggressionLevel = 0.1f;
        stateParameters[AIState.Retreat].cautionLevel = 1.0f;
        stateParameters[AIState.Retreat].urgency = 1.0f;

        stateParameters[AIState.Flanking].speedMultiplier = chaseSpeedMultiplier;
        stateParameters[AIState.Flanking].aggressionLevel = 0.6f;
        stateParameters[AIState.Flanking].cautionLevel = 0.7f;

        InitializeTransitionConditions();
    }

    void InitializeTransitionConditions()
    {
        transitionConditions[AIState.Idle] = new List<StateTransitionCondition>
        {
            new StateTransitionCondition(() => hasTarget, 10f, "Target detected", false),
            new StateTransitionCondition(() => enablePatrol && stateTimer > 1f, 5f, "Patrol available", true, 1f)
        };

        transitionConditions[AIState.Patrol] = new List<StateTransitionCondition>
        {
            new StateTransitionCondition(() => hasTarget, 10f, "Target detected", false)
        };

        transitionConditions[AIState.Chase] = new List<StateTransitionCondition>
        {
            new StateTransitionCondition(() => !hasTarget, 8f, "Target lost", true, 0.5f),
            new StateTransitionCondition(() => hasTarget && (player.position - tf.position).sqrMagnitude <= attackRangeSqr, 15f, "In attack range", false),
            new StateTransitionCondition(() => ShouldRetreat(), 20f, "Low health - retreat", false),
            new StateTransitionCondition(() => enableFlanking && ShouldFlank(), 12f, "Flanking opportunity", true, 2f)
        };

        transitionConditions[AIState.Attack] = new List<StateTransitionCondition>
        {
            new StateTransitionCondition(() => (player.position - tf.position).sqrMagnitude > attackRangeSqr * 1.5f, 10f, "Too far from target", true, 0.3f)
        };

        transitionConditions[AIState.Retreat] = new List<StateTransitionCondition>
        {
            new StateTransitionCondition(() => !ShouldRetreat() && hasTarget, 15f, "Health recovered", true, 1f),
            new StateTransitionCondition(() => !hasTarget, 8f, "Target lost", true, 0.5f)
        };

        transitionConditions[AIState.Flanking] = new List<StateTransitionCondition>
        {
            new StateTransitionCondition(() => !hasTarget || !enableFlanking, 10f, "Flanking cancelled", false),
            new StateTransitionCondition(() => (flankingTarget - tf.position).sqrMagnitude < 4f, 12f, "Flanking position reached", true, 1f),
            new StateTransitionCondition(() => ShouldRetreat(), 25f, "Low health - retreat", false)
        };
    }

    void GeneratePatrolWaypoints()
    {
        patrolWaypoints.Clear();
        for (int i = 0; i < patrolWaypointCount; i++)
        {
            float angle = (360f / patrolWaypointCount) * i * Mathf.Deg2Rad;
            Vector3 waypoint = patrolStartPosition + new Vector3(
                Mathf.Cos(angle) * patrolRadius,
                0f,
                Mathf.Sin(angle) * patrolRadius
            );
            patrolWaypoints.Add(waypoint);
        }
    }

    public void AddPatrolWaypoint(Vector3 position)
    {
        customPatrolWaypoints.Add(position);
        useCustomWaypoints = true;
        
        if (Application.isPlaying)
        {
            patrolWaypoints = new List<Vector3>(customPatrolWaypoints);
        }
        
        Debug.Log($"[Enemy Movement] Successfully added patrol waypoint at {position}. Total waypoints: {customPatrolWaypoints.Count}");
    }

    public void RemovePatrolWaypoint(int index)
    {
        if (index >= 0 && index < customPatrolWaypoints.Count)
        {
            Debug.Log($"[Enemy Movement] Successfully removed patrol waypoint at index {index}. Remaining waypoints: {customPatrolWaypoints.Count - 1}");
            customPatrolWaypoints.RemoveAt(index);
            
            if (Application.isPlaying)
            {
                patrolWaypoints = new List<Vector3>(customPatrolWaypoints);
                if (currentWaypointIndex >= patrolWaypoints.Count)
                    currentWaypointIndex = 0;
            }
        }
        else
        {
            Debug.LogWarning($"[Enemy Movement] Failed to remove waypoint - invalid index {index}. Max index: {customPatrolWaypoints.Count - 1}");
        }
    }

    public void ClearPatrolWaypoints()
    {
        int waypointCount = customPatrolWaypoints.Count;
        customPatrolWaypoints.Clear();
        useCustomWaypoints = false;
        
        if (Application.isPlaying)
        {
            patrolWaypoints.Clear();
            currentWaypointIndex = 0;
        }
        
        Debug.Log($"[Enemy Movement] Successfully cleared all patrol waypoints. Removed {waypointCount} waypoints.");
    }

    public void SwitchToCustomWaypoints()
    {
        useCustomWaypoints = true;
        patrolWaypoints = new List<Vector3>(customPatrolWaypoints);
        currentWaypointIndex = 0;
        Debug.Log($"[Enemy Movement] Successfully switched to custom waypoints. Total waypoints: {patrolWaypoints.Count}");
    }

    public void SwitchToAutoWaypoints()
    {
        useCustomWaypoints = false;
        patrolWaypoints.Clear();
        GeneratePatrolWaypoints();
        currentWaypointIndex = 0;
        Debug.Log($"[Enemy Movement] Successfully switched to auto-generated waypoints. Total waypoints: {patrolWaypoints.Count}");
    }

    public void GenerateRandomWaypoints()
    {
        int randomCount = Random.Range(minRandomWaypoints, maxRandomWaypoints + 1);
        customPatrolWaypoints.Clear();

        Vector3 centerPos = Application.isPlaying ? tf.position : transform.position;

        for (int i = 0; i < randomCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, randomWaypointSpreadRadius);
            float heightVariation = Random.Range(-2f, 2f);
            
            Vector3 randomWaypoint = centerPos + new Vector3(
                Mathf.Cos(angle) * distance,
                heightVariation,
                Mathf.Sin(angle) * distance
            );
            
            customPatrolWaypoints.Add(randomWaypoint);
        }

        useCustomWaypoints = true;
        
        if (Application.isPlaying)
        {
            patrolWaypoints = new List<Vector3>(customPatrolWaypoints);
            currentWaypointIndex = 0;
        }

        generateRandomWaypoints = false;
    }
    #endregion

    #region Player Tracking
    void UpdatePlayerTracking()
    {
        if (player == null) return;

        Vector3 currentPos = player.position;
        float timeDelta = Mathf.Max(deltaTime, 0.001f);

        playerVelocity = (currentPos - lastPlayerPos) / timeDelta;

        if (playerPositionHistory.Count > 0)
        {
            Vector3 prevVelocity = (lastPlayerPos - GetPreviousPosition(1)) / timeDelta;
            playerAcceleration = (playerVelocity - prevVelocity) / timeDelta;
        }

        smoothedPlayerVelocity = Vector3.Lerp(smoothedPlayerVelocity, playerVelocity, deltaTime * 5f);

        playerPositionHistory.Enqueue(currentPos);
        playerTimeHistory.Enqueue(currentTime);
        if (playerPositionHistory.Count > HISTORY_SIZE)
        {
            playerPositionHistory.Dequeue();
            playerTimeHistory.Dequeue();
        }

        lastPlayerPos = currentPos;
    }

    Vector3 GetPreviousPosition(int stepsBack)
    {
        if (playerPositionHistory.Count <= stepsBack) return lastPlayerPos;
        Vector3[] positions = playerPositionHistory.ToArray();
        return positions[positions.Length - 1 - stepsBack];
    }
    #endregion

    #region AI State Machine
    void UpdateAIState()
    {
        previousState = currentState;
        stateTimer += deltaTime;
        stateCooldownTimer += deltaTime;

        if (useLineOfSight && currentTime - lastLineOfSightCheck >= lineOfSightUpdateInterval)
        {
            CheckLineOfSight();
            lastLineOfSightCheck = currentTime;
        }

        UpdateDetection();

        if (useStateEvaluation && currentTime - lastStateEvaluation >= stateEvaluationInterval)
        {
            EvaluateAllStates();
            lastStateEvaluation = currentTime;
        }

        if (useStateEvaluation)
        {
            EvaluateAndTransition();
        }
        else
        {
            EvaluateSimpleTransitions();
        }

        if (stateTransitionPending && stateTimer >= minStateDuration)
        {
            TransitionToState(queuedState, "Queued transition");
            stateTransitionPending = false;
        }
    }

    void EvaluateAllStates()
    {
        foreach (AIState state in System.Enum.GetValues(typeof(AIState)))
        {
            stateScores[state] = 0f;
        }

        if (player == null) return;

        float distSqr = (player.position - tf.position).sqrMagnitude;
        float healthPercent = enemyHealth != null ? enemyHealth.GetHealthPercentage() : 1f;

        foreach (AIState state in System.Enum.GetValues(typeof(AIState)))
        {
            float score = 0f;

            switch (state)
            {
                case AIState.Idle:
                    if (!hasTarget)
                        score = 5f;
                    break;

                case AIState.Patrol:
                    if (!hasTarget && enablePatrol)
                        score = 7f;
                    break;

                case AIState.Chase:
                    if (hasTarget && distSqr > attackRangeSqr)
                    {
                        score = 15f;
                        if (hasLineOfSight) score += 5f;
                        score += Mathf.Clamp01(healthPercent) * 10f;
                    }
                    break;

                case AIState.Attack:
                    if (hasTarget && distSqr <= attackRangeSqr * 1.5f)
                    {
                        score = 20f;
                        if (distSqr <= attackRangeSqr) score += 10f;
                    }
                    break;

                case AIState.Retreat:
                    if (ShouldRetreat())
                    {
                        score = 30f + (1f - healthPercent) * 20f;
                    }
                    break;

                case AIState.Flanking:
                    if (hasTarget && enableFlanking && ShouldFlank())
                    {
                        score = 12f;
                        if (hasLineOfSight) score += 3f;
                    }
                    break;

                case AIState.Search:
                    if (isSearching && !hasTarget)
                    {
                        score = 8f + suspicionLevel * 5f;
                    }
                    break;

                case AIState.TakeCover:
                    if (hasTarget && useCoverSystem && currentCover != null && currentCover.quality > 0.6f)
                    {
                        score = 15f;
                        if (healthPercent < 0.5f) score += 10f;
                    }
                    break;
            }

            if (useStateCooldowns && currentTime - lastStateExitTime[state] < stateCooldowns[state])
            {
                score *= 0.1f;
            }

            if (stateTimer < minStateDuration && state == currentState)
            {
                score *= 0.5f;
            }

            stateScores[state] = score;
        }
    }

    void EvaluateAndTransition()
    {
        AIState bestState = currentState;
        float bestScore = stateScores[currentState];

        int currentPriority = useStatePriorities && statePriorities.ContainsKey(currentState) 
            ? statePriorities[currentState] 
            : 0;

        foreach (AIState candidateState in System.Enum.GetValues(typeof(AIState)))
        {
            if (candidateState == currentState) continue;

            float candidateScore = stateScores[candidateState];
            int candidatePriority = useStatePriorities && statePriorities.ContainsKey(candidateState) 
                ? statePriorities[candidateState] 
                : 0;

            if (useStatePriorities)
            {
                if (candidatePriority < currentPriority && candidateScore < bestScore * (1f + stateTransitionHysteresis))
                {
                    continue;
                }
            }

            if (useStateCooldowns)
            {
                if (currentTime - lastStateExitTime[candidateState] < stateCooldowns[candidateState])
                {
                    continue;
                }
            }

            if (stateTimer < minStateDuration && candidateState != currentState)
            {
                if (candidateScore > bestScore * 1.5f && candidatePriority > currentPriority)
                {
                    queuedState = candidateState;
                    stateTransitionPending = true;
                }
                continue;
            }

            if (transitionConditions.ContainsKey(currentState))
            {
                float totalWeight = 0f;
                foreach (var condition in transitionConditions[currentState])
                {
                    if (condition.condition != null && condition.condition())
                    {
                        if (!condition.requiresMinimumTime || stateTimer >= condition.minimumTime)
                            totalWeight += condition.weight;
                    }
                }
                if (totalWeight < 0.01f)
                    continue;
            }

            if (candidateScore > bestScore)
            {
                bestState = candidateState;
                bestScore = candidateScore;
            }
        }

        if (bestState != currentState && bestScore > stateScores[currentState] * (1f + stateTransitionHysteresis))
        {
            TransitionToState(bestState, "State evaluation");
        }
    }

    void EvaluateSimpleTransitions()
    {
        switch (currentState)
        {
            case AIState.Idle:
                if (hasTarget)
                {
                    TransitionToState(AIState.Chase, "Target detected");
                }
                else if (enablePatrol)
                {
                    TransitionToState(AIState.Patrol, "Start patrol");
                }
                break;

            case AIState.Patrol:
                if (hasTarget)
                {
                    TransitionToState(AIState.Chase, "Target detected");
                }
                break;

            case AIState.Chase:
                if (!hasTarget)
                {
                    TransitionToState(enablePatrol ? AIState.Patrol : AIState.Idle, "Target lost");
                }
                else
                {
                    float distSqr = (player.position - tf.position).sqrMagnitude;
                    if (distSqr <= attackRangeSqr)
                    {
                        TransitionToState(AIState.Attack, "In attack range");
                    }
                    else if (ShouldRetreat())
                    {
                        TransitionToState(AIState.Retreat, "Low health");
                    }
                    else if (enableFlanking && ShouldFlank())
                    {
                        TransitionToState(AIState.Flanking, "Flanking opportunity");
                    }
                }
                break;

            case AIState.Attack:
                float attackDistSqr = (player.position - tf.position).sqrMagnitude;
                if (attackDistSqr > attackRangeSqr * 1.5f)
                {
                    TransitionToState(AIState.Chase, "Too far from target");
                }
                break;

            case AIState.Retreat:
                if (!ShouldRetreat() && hasTarget)
                {
                    TransitionToState(AIState.Chase, "Health recovered");
                }
                else if (!hasTarget)
                {
                    TransitionToState(enablePatrol ? AIState.Patrol : AIState.Idle, "Target lost");
                }
                break;

            case AIState.Flanking:
                if (!hasTarget || !enableFlanking)
                {
                    TransitionToState(AIState.Chase, "Flanking cancelled");
                }
                else if ((flankingTarget - tf.position).sqrMagnitude < 4f)
                {
                    TransitionToState(AIState.Chase, "Flanking position reached");
                }
                break;

            case AIState.Search:
                if (hasTarget)
                    TransitionToState(AIState.Chase, "Target found");
                else if (!isSearching || suspicionLevel < suspicionThreshold * 0.3f)
                    TransitionToState(enablePatrol ? AIState.Patrol : AIState.Idle, "Search abandoned");
                break;

            case AIState.IsIt:
                if (!useTagGameMode || !isCurrentlyIt)
                    TransitionToState(enablePatrol ? AIState.Patrol : AIState.Idle, "No longer it");
                break;

            case AIState.Tagged:
                break;
        }
    }

    string GetStateName(AIState state)
    {
        return state.ToString();
    }

    void TransitionToState(AIState newState, string reason = "Unknown")
    {
        if (currentState == newState) return;

        if (stateHistory.Count > 0)
        {
            var lastRecord = stateHistory[stateHistory.Count - 1];
            lastRecord.duration = currentTime - lastRecord.timestamp;
        }

        StateTransitionRecord record = new StateTransitionRecord(
            currentState,
            newState,
            currentTime,
            0f,
            reason
        );
        stateHistory.Add(record);

        while (stateHistory.Count > MAX_STATE_HISTORY)
        {
            stateHistory.RemoveAt(0);
        }

        stateHistory.RemoveAll(r => currentTime - r.timestamp > stateHistoryMemory);

        lastStateExitTime[currentState] = currentTime;

        OnStateExit(currentState);
        previousState = currentState;
        currentState = newState;
        stateTimer = 0f;
        stateCooldownTimer = 0f;
        stateTransitionPending = false;
        OnStateEnter(newState);
    }

    void OnStateEnter(AIState state)
    {
        switch (state)
        {
            case AIState.Chase:
                if (alertClip && !wasChasing)
                    audioSource.PlayOneShot(alertClip, alertVolume);
                wasChasing = true;
                break;

            case AIState.Flanking:
                CalculateFlankingPosition();
                isFlanking = true;
                flankingTimer = 0f;
                break;

            case AIState.Retreat:
                isFlanking = false;
                break;
        }
    }

    void OnStateExit(AIState state)
    {
        switch (state)
        {
            case AIState.Flanking:
                isFlanking = false;
                break;
        }
    }

    void ExecuteCurrentState()
    {
        if (isDashing)
        {
            HandleDash();
            return;
        }

        switch (currentState)
        {
            case AIState.Idle:
                HandleIdle();
                break;
            case AIState.Patrol:
                HandlePatrol();
                break;
            case AIState.Chase:
                HandleChase();
                break;
            case AIState.Attack:
                HandleAttack();
                break;
            case AIState.Retreat:
                HandleRetreat();
                break;
            case AIState.Flanking:
                HandleFlanking();
                break;
            case AIState.Search:
                HandleSearching();
                break;
            case AIState.IsIt:
                HandleIsItState();
                break;
            case AIState.Tagged:
                HandleTaggedState();
                break;
            case AIState.TakeCover:
                if (!useTagGameMode && useCoverSystem)
                {
                    if (currentCover != null && !isInCover)
                        MoveToCover();
                    else if (isInCover)
                        HandleCoverBehavior();
                    else
                        HandleChase();
                }
                else
                {
                    HandleChase();
                }
                break;
        }
    }
    #endregion

    #region Detection
    void UpdateDetection()
    {
        if (player == null) return;

        float distSqr = (player.position - tf.position).sqrMagnitude;

        if (!hasTarget && distSqr < detectRangeSqr)
        {
            if (!useLineOfSight || hasLineOfSight)
            {
                hasTarget = true;
            }
        }
        else if (hasTarget && distSqr > loseRangeSqr)
        {
            hasTarget = false;
        }
    }

    void CheckLineOfSight()
    {
        if (player == null)
        {
            hasLineOfSight = false;
            return;
        }

        tempVector = player.position - tf.position;
        float distance = tempVector.magnitude;

        if (distance > detectRange)
        {
            hasLineOfSight = false;
            return;
        }

        RaycastHit hit;
        hasLineOfSight = !Physics.Raycast(tf.position + Vector3.up, tempVector.normalized, out hit, distance, obstacleLayer);
    }

    bool ShouldRetreat()
    {
        if (enemyHealth == null) return false;
        return enemyHealth.GetHealthPercentage() < retreatHealthThreshold;
    }

    bool ShouldFlank()
    {
        if (!hasTarget || !hasLineOfSight) return false;
        if (stateTimer < 2f) return false;

        float distSqr = (player.position - tf.position).sqrMagnitude;
        return distSqr > attackRangeSqr * 4f && distSqr < detectRangeSqr * 0.5f;
    }
    #endregion

    #region Advanced Sensory System
    void UpdateAdvancedSensorySystem()
    {
        if (player == null) return;

        UpdateVisionSystem();
        UpdateHearingSystem();
        UpdateSuspicionSystem();
        UpdateMemorySystem();
    }

    void UpdateVisionSystem()
    {
        if (!useVisionCone) return;

        Vector3 toPlayer = player.position - tf.position;
        float distSqr = toPlayer.sqrMagnitude;
        toPlayer.y = 0f;
        Vector3 forward = tf.forward;
        forward.y = 0f;

        bool canSeePlayer = false;

        if (distSqr < visionRangeSqr)
        {
            float angle = Vector3.Angle(forward, toPlayer.normalized);
            if (angle <= visionAngle * 0.5f)
            {
                RaycastHit hit;
                if (!Physics.Raycast(tf.position + Vector3.up, toPlayer.normalized, out hit, Mathf.Sqrt(distSqr), obstacleLayer))
                {
                    canSeePlayer = true;
                    hasLineOfSight = true;
                }
            }
            else if (distSqr < peripheralVisionRangeSqr && angle <= peripheralVisionAngle * 0.5f)
            {
                RaycastHit hit;
                if (!Physics.Raycast(tf.position + Vector3.up, toPlayer.normalized, out hit, Mathf.Sqrt(distSqr), obstacleLayer))
                {
                    canSeePlayer = true;
                    hasLineOfSight = true;
                    suspicionLevel = Mathf.Min(suspicionLevel + 0.3f * deltaTime, maxSuspicion);
                }
            }
        }

        if (canSeePlayer)
        {
            hasTarget = true;
            lastKnownPlayerPosition = player.position;
            lastKnownPositionTime = currentTime;
            suspicionLevel = Mathf.Min(suspicionLevel + 0.5f * deltaTime, maxSuspicion);
        }
        else
        {
            hasLineOfSight = false;
        }
    }

    void UpdateHearingSystem()
    {
        if (!useHearing) return;

        recentSounds.RemoveAll(s => currentTime - s.timestamp > 5f);

        if (player != null && hasTarget)
        {
            float playerSpeed = playerVelocity.magnitude;
            if (playerSpeed > 3f)
            {
                float distSqr = (player.position - tf.position).sqrMagnitude;
                float hearingRangeSqr = hearingRange * hearingRange;

                if (distSqr < hearingRangeSqr)
                {
                    float volume = Mathf.Clamp01(1f - (distSqr / hearingRangeSqr)) * (playerSpeed / 10f);
                    RegisterSound(player.position, volume, "footsteps");
                }
            }
        }
    }

    public void RegisterSound(Vector3 position, float volume, string sourceType)
    {
        if (!useHearing)
        {
            Debug.LogWarning("[Enemy Movement] Sound registration failed - hearing system is disabled");
            return;
        }

        float distSqr = (position - tf.position).sqrMagnitude;
        float hearingRangeSqr = hearingRange * hearingRange;

        if (distSqr < hearingRangeSqr)
        {
            recentSounds.Add(new SoundEvent(position, volume, currentTime, sourceType));

            float suspicionIncrease = volume * (1f - (distSqr / hearingRangeSqr));
            suspicionLevel = Mathf.Min(suspicionLevel + suspicionIncrease * 0.3f, maxSuspicion);

            if (suspicionLevel >= suspicionThreshold && !hasTarget)
            {
                lastKnownPlayerPosition = position;
                lastKnownPositionTime = currentTime;
                StartSearching(position);
                Debug.Log($"[Enemy Movement] Sound registered ({sourceType}, volume: {volume:F2}) - starting investigation at {position}");
            }
            else
            {
                Debug.Log($"[Enemy Movement] Sound registered ({sourceType}, volume: {volume:F2}). Suspicion level: {suspicionLevel:F2}");
            }

            if (recentSounds.Count > MAX_SOUND_MEMORY)
            {
                recentSounds.RemoveAt(0);
            }
        }
        else
        {
            Debug.LogWarning($"[Enemy Movement] Sound at {position} is out of hearing range (distance: {Mathf.Sqrt(distSqr):F2}, range: {hearingRange})");
        }
    }

    void UpdateSuspicionSystem()
    {
        if (suspicionLevel > 0f)
        {
            suspicionLevel = Mathf.Max(0f, suspicionLevel - suspicionDecayRate * deltaTime);
        }

        if (suspicionLevel < suspicionThreshold * 0.5f && !hasTarget)
        {
            isSearching = false;
        }
    }

    void UpdateMemorySystem()
    {
        if (!useMemorySystem) return;

        if (hasTarget && player != null)
        {
            lastKnownPlayerPosition = player.position;
            lastKnownPositionTime = currentTime;
        }

        if (hasTarget && !hasLineOfSight)
        {
            if (currentTime - lastKnownPositionTime > lastKnownPositionMemory)
            {
                hasTarget = false;
                if (suspicionLevel > suspicionThreshold)
                {
                    StartSearching(lastKnownPlayerPosition);
                }
            }
        }

        if (isSearching && currentState != AIState.Search)
            TransitionToState(AIState.Search, "Memory search");
    }

    void StartSearching(Vector3 startPosition)
    {
        isSearching = true;
        searchPoints.Clear();
        currentSearchPointIndex = 0;
        searchPointWaitTimer = 0f;

        if (currentState != AIState.Search)
            TransitionToState(AIState.Search, "Starting search");

        Vector3 center = startPosition;
        for (int i = 0; i < maxSearchPoints; i++)
        {
            float angle = (360f / maxSearchPoints) * i * Mathf.Deg2Rad;
            float radius = searchRadius * (0.5f + Random.Range(0f, 0.5f));
            Vector3 searchPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            RaycastHit hit;
            if (!Physics.Raycast(searchPoint + Vector3.up * 10f, Vector3.down, out hit, 20f, obstacleLayer))
            {
                searchPoints.Add(searchPoint);
            }
        }

        if (searchPoints.Count == 0)
        {
            searchPoints.Add(center);
        }
    }

    void HandleSearching()
    {
        if (searchPoints.Count == 0 || hasTarget)
        {
            isSearching = false;
            return;
        }

        Vector3 currentSearchPoint = searchPoints[currentSearchPointIndex];
        tempVector = currentSearchPoint - tf.position;
        tempVector.y = 0f;

        if (tempVector.sqrMagnitude < 4f)
        {
            searchPointWaitTimer += deltaTime;
            if (searchPointWaitTimer >= searchPointWaitTime)
            {
                currentSearchPointIndex = (currentSearchPointIndex + 1) % searchPoints.Count;
                searchPointWaitTimer = 0f;
            }
        }
        else
        {
            MoveTowards(currentSearchPoint, moveSpeed * 0.8f);
        }

        if (player != null)
        {
            float distSqr = (player.position - tf.position).sqrMagnitude;
            if (distSqr < detectRangeSqr && CheckLineOfSightToPosition(player.position))
            {
                hasTarget = true;
                isSearching = false;
            }
        }
    }

    bool CheckLineOfSightToPosition(Vector3 position)
    {
        tempVector = position - tf.position;
        float distance = tempVector.magnitude;
        RaycastHit hit;
        return !Physics.Raycast(tf.position + Vector3.up, tempVector.normalized, out hit, distance, obstacleLayer);
    }
    #endregion

    #region Cover System
    void UpdateCoverSystem()
    {
        if (!useCoverSystem || !hasTarget || player == null) return;

        if (currentTime - lastCoverEvaluation >= coverEvaluationInterval)
        {
            EvaluateCover();
            lastCoverEvaluation = currentTime;
        }

        if (currentCover != null && !isInCover)
        {
            MoveToCover();
        }
        else if (isInCover)
        {
            HandleCoverBehavior();
        }
    }

    void EvaluateCover()
    {
        availableCovers.Clear();
        Vector3 threatPosition = player.position;

        Collider[] potentialCovers = Physics.OverlapSphere(tf.position, coverDetectionRange, coverLayer);
        
        foreach (Collider col in potentialCovers)
        {
            if (col.transform == tf) continue;

            Vector3 coverPos = col.bounds.center;
            Vector3 toThreat = threatPosition - coverPos;
            toThreat.y = 0f;
            float distToCover = (coverPos - tf.position).magnitude;
            float distToThreat = toThreat.magnitude;

            Vector3 toCover = coverPos - tf.position;
            toCover.y = 0f;
            float angle = Vector3.Angle(toCover.normalized, toThreat.normalized);

            if (angle > 90f && distToCover < coverDetectionRange)
            {
                float quality = EvaluateCoverQuality(coverPos, threatPosition, col);
                availableCovers.Add(new CoverInfo(coverPos, toThreat.normalized, quality, distToCover, col));
            }
        }

        availableCovers.Sort((a, b) => 
        {
            float scoreA = a.quality * 0.7f + (1f - a.distance / coverDetectionRange) * 0.3f;
            float scoreB = b.quality * 0.7f + (1f - b.distance / coverDetectionRange) * 0.3f;
            return scoreB.CompareTo(scoreA);
        });

        if (availableCovers.Count > 0 && (currentCover == null || availableCovers[0].quality > currentCover.quality))
        {
            currentCover = availableCovers[0];
        }
    }

    float EvaluateCoverQuality(Vector3 coverPos, Vector3 threatPos, Collider coverCollider)
    {
        float quality = 0.5f;

        float coverHeight = coverCollider.bounds.size.y;
        if (coverHeight >= coverMinHeight && coverHeight <= coverMaxHeight)
        {
            quality += 0.3f;
        }

        float distToThreat = (threatPos - coverPos).magnitude;
        if (distToThreat < 15f)
        {
            quality += 0.2f;
        }

        RaycastHit hit;
        if (Physics.Raycast(threatPos + Vector3.up, (coverPos - threatPos).normalized, out hit, (coverPos - threatPos).magnitude, obstacleLayer))
        {
            if (hit.collider == coverCollider)
            {
                quality += 0.3f;
            }
        }

        return Mathf.Clamp01(quality);
    }

    void MoveToCover()
    {
        if (currentCover == null) return;

        Vector3 targetPos = currentCover.position - currentCover.direction * 1f;
        tempVector = targetPos - tf.position;
        tempVector.y = 0f;

        if (tempVector.sqrMagnitude < 2f)
        {
            isInCover = true;
            coverPosition = targetPos;
        }
        else
        {
            MoveTowards(targetPos, moveSpeed * 0.9f);
        }
    }

    void HandleCoverBehavior()
    {
        if (currentCover == null || !isInCover) return;

        coverPeekTimer += deltaTime;
        if (coverPeekTimer > 2f && hasLineOfSight)
        {
            Vector3 peekDirection = currentCover.direction * -1f;
            Vector3 peekPos = coverPosition + peekDirection * peekFromCoverDistance;
            MoveTowards(peekPos, moveSpeed * 0.5f);
            
            if (coverPeekTimer > 3f)
            {
                coverPeekTimer = 0f;
            }
        }

        if (player != null)
        {
            float distToThreat = (player.position - tf.position).magnitude;
            if (distToThreat < minCombatDistance || !CheckLineOfSightToPosition(player.position))
            {
                isInCover = false;
                currentCover = null;
            }
        }
    }
    #endregion

    #region Threat Assessment
    void UpdateThreatAssessment()
    {
        if (!useThreatAssessment) return;

        if (currentTime - lastThreatEvaluation >= threatEvaluationInterval)
        {
            EvaluateThreats();
            lastThreatEvaluation = currentTime;
        }
    }

    void EvaluateThreats()
    {
        if (player == null) return;

        float threatLevel = CalculateThreatLevel(player);
        
        if (hasTarget)
        {
            trackedThreats[player] = new ThreatInfo(
                player,
                threatLevel,
                (player.position - tf.position).magnitude,
                player.position,
                currentTime,
                hasLineOfSight,
                1f
            );
            primaryThreat = player;
            currentThreatLevel = threatLevel;
        }

        if (trackMultipleTargets)
        {
            // Track other potential threats
        }

        List<Transform> toRemove = new List<Transform>();
        foreach (var kvp in trackedThreats)
        {
            if (kvp.Value.threat == null || currentTime - kvp.Value.lastSeenTime > 10f)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            trackedThreats.Remove(key);
        }
    }

    float CalculateThreatLevel(Transform threat)
    {
        if (threat == null) return 0f;

        float threatLevel = 0.5f;

        float dist = (threat.position - tf.position).magnitude;
        float distFactor = 1f - Mathf.Clamp01(dist / maxThreatDistance);
        threatLevel += distFactor * 0.3f;

        if (hasLineOfSight)
        {
            threatLevel += 0.2f;
        }

        if (player != null && threat == player)
        {
            Vector3 toThreat = threat.position - tf.position;
            toThreat.y = 0f;
            float approachSpeed = Vector3.Dot(playerVelocity.normalized, toThreat.normalized);
            if (approachSpeed > 0.3f)
            {
                threatLevel += 0.2f;
            }
        }

        return Mathf.Clamp01(threatLevel);
    }
    #endregion

    #region Tactical Positioning
    void UpdateTacticalPositioning()
    {
        if (!useTacticalPositioning || !hasTarget || player == null) return;

        EvaluateTacticalPosition();
    }

    void EvaluateTacticalPosition()
    {
        Vector3 threatPos = player.position;
        Vector3 currentPos = tf.position;
        Vector3 toThreat = threatPos - currentPos;
        toThreat.y = 0f;
        float distToThreat = toThreat.magnitude;

        Vector3 idealDirection = toThreat.normalized;
        
        if (distToThreat < minCombatDistance)
        {
            tacticalTargetPosition = currentPos - idealDirection * (preferredCombatDistance - distToThreat);
        }
        else if (distToThreat > maxCombatDistance)
        {
            tacticalTargetPosition = currentPos + idealDirection * (distToThreat - preferredCombatDistance);
        }
        else
        {
            tacticalTargetPosition = CalculateOptimalPosition(threatPos, currentPos);
        }

        tacticalPositionEvaluation = EvaluatePositionQuality(tacticalTargetPosition, threatPos);
        hasTacticalPosition = tacticalPositionEvaluation > 0.3f;
    }

    Vector3 CalculateOptimalPosition(Vector3 threatPos, Vector3 currentPos)
    {
        Vector3 optimalPos = currentPos;
        Vector3 toThreat = threatPos - currentPos;
        toThreat.y = 0f;

        if (preferHeightAdvantage)
        {
            Vector3[] testDirections = new Vector3[]
            {
                tf.right,
                -tf.right,
                tf.forward,
                -tf.forward,
                (tf.right + tf.forward).normalized,
                (-tf.right + tf.forward).normalized
            };

            float bestHeight = currentPos.y;
            Vector3 bestPos = currentPos;

            foreach (Vector3 dir in testDirections)
            {
                Vector3 testPos = currentPos + dir * 5f;
                RaycastHit hit;
                if (Physics.Raycast(testPos + Vector3.up * 10f, Vector3.down, out hit, 20f))
                {
                    if (hit.point.y > bestHeight)
                    {
                        bestHeight = hit.point.y;
                        bestPos = testPos;
                        bestPos.y = hit.point.y;
                    }
                }
            }

            if (bestHeight > currentPos.y + 0.5f)
            {
                optimalPos = bestPos;
            }
        }

        return optimalPos;
    }

    float EvaluatePositionQuality(Vector3 position, Vector3 threatPosition)
    {
        float quality = 0.5f;

        float dist = (position - threatPosition).magnitude;
        float distFromOptimal = Mathf.Abs(dist - preferredCombatDistance);
        float distQuality = 1f - Mathf.Clamp01(distFromOptimal / preferredCombatDistance);
        quality += distQuality * 0.3f;

        if (preferHeightAdvantage)
        {
            float heightDiff = position.y - threatPosition.y;
            if (heightDiff > 0.5f)
            {
                quality += Mathf.Clamp01(heightDiff / 5f) * heightAdvantageWeight * 0.2f;
            }
        }

        if (CheckLineOfSightToPosition(threatPosition))
        {
            quality += 0.2f;
        }

        return Mathf.Clamp01(quality);
    }
    #endregion

    #region State Handlers
    void HandleIdle()
    {
        ApplyGravity();
        MoveCharacter(velocity * deltaTime);
    }

    void HandlePatrol()
    {
        if (patrolWaypoints.Count == 0) return;

        Vector3 targetWaypoint = patrolWaypoints[currentWaypointIndex];
        tempVector = targetWaypoint - tf.position;
        tempVector.y = 0f;

        if (tempVector.sqrMagnitude < 4f)
        {
            patrolWaitTimer += deltaTime;
            if (patrolWaitTimer >= patrolWaitTime)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Count;
                patrolWaitTimer = 0f;
            }
        }
        else
        {
            MoveTowards(targetWaypoint, moveSpeed);
        }
    }

    void HandleChase()
    {
        if (!hasTarget || player == null)
        {
            if (useMemorySystem && isSearching)
            {
                HandleSearching();
                return;
            }
            return;
        }

        Vector3 targetPos = PredictPlayerPosition();
        StateParameters parameters = stateParameters[AIState.Chase];
        
        if (useTacticalPositioning && hasTacticalPosition && tacticalPositionEvaluation > 0.5f)
        {
            targetPos = Vector3.Lerp(targetPos, tacticalTargetPosition, 0.3f);
        }
        
        float speed = moveSpeed * parameters.speedMultiplier;
        
        if (parameters.aggressionLevel > 0.7f)
        {
            targetPos = Vector3.Lerp(targetPos, player.position, 0.3f);
        }
        
        MoveTowards(targetPos, speed);
        bool canDash = !useTagGameMode || !enableStaminaSystem || currentStamina > minStaminaForDash;
        if (canDash)
            TryDash(targetPos);
    }

    void HandleAttack()
    {
        if (!hasTarget || player == null) return;

        StateParameters parameters = stateParameters[AIState.Attack];
        Vector3 targetPos = player.position + parameters.targetOffset;
        
        float speed = moveSpeed * parameters.speedMultiplier;
        MoveTowards(targetPos, speed);
    }

    void HandleRetreat()
    {
        if (!hasTarget || player == null) return;

        StateParameters parameters = stateParameters[AIState.Retreat];
        
        tempVector = tf.position - player.position;
        tempVector.y = 0f;
        tempVector.Normalize();

        float retreatDist = 10f + parameters.cautionLevel * 10f;
        Vector3 retreatPos = tf.position + tempVector * retreatDist;
        
        float speed = moveSpeed * parameters.speedMultiplier;
        MoveTowards(retreatPos, speed);
    }

    void HandleFlanking()
    {
        if (!isFlanking || !hasTarget) return;

        flankingTimer += deltaTime;
        if (flankingTimer > 5f)
        {
            CalculateFlankingPosition();
            flankingTimer = 0f;
        }

        MoveTowards(flankingTarget, moveSpeed * chaseSpeedMultiplier);
    }

    void CalculateFlankingPosition()
    {
        if (player == null) return;

        tempVector = player.position - tf.position;
        tempVector.y = 0f;
        float distance = tempVector.magnitude;

        if (distance < 0.1f) return;

        Vector3 toPlayer = tempVector.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toPlayer);

        float side = (flankingTimer % 2f < 1f) ? 1f : -1f;
        Vector3 flankDirection = (toPlayer + right * side * Mathf.Sin(flankingAngle * Mathf.Deg2Rad)).normalized;

        flankingTarget = player.position + flankDirection * flankingDistance;
    }

    void HandleIsItState()
    {
        Transform target = GetClosestVulnerablePlayer();
        
        if (target == null)
        {
            if (enablePatrol)
                HandlePatrol();
            else
                HandleIdle();
            return;
        }

        float speed = moveSpeed * chaseSpeedMultiplier;
        speed = GetAdjustedMoveSpeed(speed);

        Vector3 targetPos = (target == player) ? PredictPlayerPosition() : PredictTargetPosition(target);

        if (enableCooperativeChasing && nearbyEnemies.Count > 1 && reduceFlankingComplexity)
        {
            float angle = ((float)nearbyEnemies.IndexOf(this) / nearbyEnemies.Count) * 360f * Mathf.Deg2Rad;
            Vector3 cooperativeFlankDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 cooperativePosition = target.position + cooperativeFlankDir * cooperativeFlankDistance;
            targetPos = Vector3.Lerp(targetPos, cooperativePosition, 0.4f);
        }

        MoveTowards(targetPos, speed);

        if (currentStamina > minStaminaForDash)
            TryDash(targetPos);

        lastOtherTargetPosition = target.position;
        lastOtherTargetTime = currentTime;
    }

    void HandleTaggedState()
    {
        if (stateTimer > 0.5f)
        {
            TransitionToState(AIState.Retreat, "Tagged - retreating");
        }
        else
        {
            velocity.x = 0f;
            velocity.z = 0f;
            ApplyGravity();
            MoveCharacter(velocity * deltaTime);
        }
    }
    #endregion

    #region Movement
    void MoveTowards(Vector3 target, float speed)
    {
        if (enableObstacleEscape)
            UpdateJumpState();

        if (isJumping)
        {
            ApplyGravity();
            MoveCharacter(velocity * deltaTime);
            return;
        }

        tempVector = target - tf.position;
        tempVector.y = 0f;

        if (useTagGameMode)
        {
            speed = GetAdjustedMoveSpeed(speed);
        }

        float sqrMag = tempVector.sqrMagnitude;
        if (sqrMag > minMoveDistance * minMoveDistance)
        {
            tempVector *= 1f / Mathf.Sqrt(sqrMag);

            ApplyObstacleAvoidance(ref tempVector);

            velocity.x = tempVector.x * speed;
            velocity.z = tempVector.z * speed;

            Quaternion rot = Quaternion.LookRotation(tempVector);
            tf.rotation = Quaternion.Slerp(tf.rotation, rot, rotationSpeed * deltaTime);
        }
        else
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }

        ApplyGravity();
        MoveCharacter(velocity * deltaTime);
    }

    void ApplyObstacleAvoidance(ref Vector3 direction)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(tf.position, obstacleAvoidanceRadius, obstacleBuffer, obstacleLayer);
        if (hitCount == 0) return;

        tempVector2 = Vector3.zero;
        for (int i = 0; i < hitCount; i++)
        {
            Transform obsT = obstacleBuffer[i].transform;
            if (obsT == tf) continue;
            if (player != null && obsT == player) continue;
            if (allPlayers != null && allPlayers.Contains(obsT)) continue;

            Vector3 toObstacle = tf.position - obsT.position;
            toObstacle.y = 0f;
            float dist = toObstacle.magnitude;
            if (dist < 0.1f) continue;

            float force = (obstacleAvoidanceRadius - dist) / obstacleAvoidanceRadius;
            force = Mathf.Clamp01(force);
            tempVector2 += toObstacle.normalized * force;
        }

        if (tempVector2.sqrMagnitude > 0.01f)
        {
            tempVector2.Normalize();
            float blend = Mathf.Clamp01(obstacleAvoidanceForce * deltaTime * 2f);
            direction = Vector3.Lerp(direction, tempVector2, blend).normalized;
        }
    }

    void ApplyGravity()
    {
        if (!cc.isGrounded)
            velocity.y -= gravity * deltaTime;
        else if (velocity.y < 0f)
            velocity.y = -2f;
    }
    #endregion

    #region Prediction
    Vector3 PredictPlayerPosition()
    {
        if (player == null) return tf.position;

        Vector3 predictedPos = player.position;

        switch (predictionMethod)
        {
            case PredictionMethod.Linear:
                predictedPos = player.position + smoothedPlayerVelocity * predictionTime;
                break;

            case PredictionMethod.Quadratic:
                predictedPos = player.position + smoothedPlayerVelocity * predictionTime +
                              playerAcceleration * predictionTime * predictionTime * 0.5f;
                break;

            case PredictionMethod.Adaptive:
                predictedPos = player.position + smoothedPlayerVelocity * adaptivePredictionTime +
                              playerAcceleration * adaptivePredictionTime * adaptivePredictionTime * 0.5f;
                UpdateAdaptivePrediction();
                break;
        }

        return predictedPos;
    }

    void UpdateAdaptivePrediction()
    {
        if (playerPositionHistory.Count < 3) return;

        Vector3 actualPos = player.position;
        Vector3 predictedPos = GetPreviousPosition(0) + smoothedPlayerVelocity * adaptivePredictionTime;

        predictionError = (actualPos - predictedPos).magnitude;

        if (predictionError > lastPredictionError)
        {
            adaptivePredictionTime = Mathf.Lerp(adaptivePredictionTime, predictionTime * 0.5f, predictionAdaptationRate);
        }
        else
        {
            adaptivePredictionTime = Mathf.Lerp(adaptivePredictionTime, predictionTime * 1.5f, predictionAdaptationRate);
        }

        adaptivePredictionTime = Mathf.Clamp(adaptivePredictionTime, predictionTime * 0.3f, predictionTime * 2f);
        lastPredictionError = predictionError;
    }

    Vector3 PredictTargetPosition(Transform target)
    {
        if (target == null) return tf.position;
        if (target == player) return PredictPlayerPosition();
        if (lastOtherTargetTime < 0f || currentTime - lastOtherTargetTime > 0.5f) return target.position;
        float dt = currentTime - lastOtherTargetTime;
        if (dt < 0.001f) return target.position;
        Vector3 vel = (target.position - lastOtherTargetPosition) / dt;
        return target.position + vel * predictionTime;
    }
    #endregion

    #region Dash
    void TryDash(Vector3 target)
    {
        if (currentTime - lastDash < dashCooldown) return;
        if (!cc.isGrounded) return;

        float distSqr = (target - tf.position).sqrMagnitude;
        if (distSqr > dashMinDistance * dashMinDistance && distSqr < dashMaxDistance * dashMaxDistance)
        {
            float optimalDistSqr = dashOptimalDistance * dashOptimalDistance;
            float distFromOptimal = Mathf.Abs(distSqr - optimalDistSqr);

            tempVector = target - tf.position;
            tempVector.y = 0f;
            tempVector.Normalize();
            float playerMovingAway = Vector3.Dot(smoothedPlayerVelocity.normalized, tempVector);

            if (distFromOptimal < 25f || playerMovingAway > 0.3f)
            {
                StartDash(tempVector);
            }
        }
    }

    void StartDash(Vector3 dir)
    {
        isDashing = true;
        dashTimer = 0f;
        lastDash = currentTime;

        dir.y = 0f;
        dir.Normalize();

        velocity.x = dir.x * dashSpeed;
        velocity.z = dir.z * dashSpeed;
        velocity.y = 0f;

        if (dashClip)
            audioSource.PlayOneShot(dashClip, dashVolume);
    }

    void HandleDash()
    {
        dashTimer += deltaTime;

        ApplyGravity();
        MoveCharacter(velocity * deltaTime);

        if (dashTimer >= dashTime)
            isDashing = false;
    }
    #endregion

    #region Audio
    void HandleAudio()
    {
        HandleFootsteps();
        HandleLandingAudio();

        if (hasTarget)
        {
            if (!wasChasing)
            {
                wasChasing = true;
            }
        }
        else
        {
            if (wasChasing)
            {
                wasChasing = false;
            }
            PlayIdleAudio();
        }
    }

    void HandleFootsteps()
    {
        if (!cc.isGrounded || velocity.sqrMagnitude < 0.1f) return;
        if (currentTime < nextFootstep) return;

        if (footstepClip)
            audioSource.PlayOneShot(footstepClip, footstepVolume);

        float speedRatio = Mathf.Max(velocity.magnitude / moveSpeed, 0.5f);
        nextFootstep = currentTime + footstepRate / speedRatio;
    }

    void HandleLandingAudio()
    {
        if (!wasGrounded && cc.isGrounded)
        {
            if (landClip)
                audioSource.PlayOneShot(landClip, landVolume);
        }
        wasGrounded = cc.isGrounded;
    }

    void PlayIdleAudio()
    {
        if (!cc.isGrounded || velocity.sqrMagnitude > 0.1f || !idleClip) return;
        if (!audioSource.isPlaying)
            audioSource.PlayOneShot(idleClip, idleVolume);
    }
    #endregion

    #region Obstacle Escape System
    void UpdateStuckDetection()
    {
        if (currentTime >= nextStuckCheck)
        {
            float distanceMoved = Vector3.Distance(tf.position, lastPosition);
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            bool wantedToMove = lastDesiredHorizontalSpeed > 0.5f;
            bool barelyMoved = distanceMoved < stuckDetectionThreshold;

            if (barelyMoved && wantedToMove && cc.isGrounded)
            {
                stuckTimer += stuckCheckRate;
                if (stuckTimer >= maxStuckTime)
                {
                    AttemptEscapeJump();
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            lastPosition = tf.position;
            nextStuckCheck = currentTime + stuckCheckRate;
            lastDesiredHorizontalSpeed = horizontalSpeed;
        }
    }

    void AttemptEscapeJump()
    {
        if (!cc.isGrounded || isJumping) return;

        PerformJump(Vector3.up);

        if (useMultipleJumpAttempts && player != null)
        {
            for (int i = 1; i < maxJumpAttempts; i++)
            {
                Vector3 toPlayer = (player.position - tf.position).normalized;
                Vector3 jumpDir = (toPlayer * 0.5f + Vector3.up * 0.5f).normalized;

                float angle = (i * 360f / maxJumpAttempts) * Mathf.Deg2Rad;
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                jumpDir = rotation * jumpDir;

                PerformJump(jumpDir);

                if (isJumping) break;
            }
        }
    }

    void PerformJump(Vector3 jumpDirection)
    {
        if (isJumping || !cc.isGrounded) return;

        float jumpVel = Mathf.Sqrt(2f * gravity * jumpHeight);

        velocity.x = jumpDirection.x * jumpVel * 0.5f;
        velocity.y = jumpVel;
        velocity.z = jumpDirection.z * jumpVel * 0.5f;

        isJumping = true;
        stuckTimer = 0f;

        if (dashClip)
            audioSource.PlayOneShot(dashClip, dashVolume * 0.6f);
    }

    void UpdateJumpState()
    {
        if (cc.isGrounded && isJumping)
        {
            isJumping = false;
            stuckTimer = 0f;
        }
    }
    #endregion

    #region Tag Game Systems
    void UpdateStaminaSystem()
    {
        if (!enableStaminaSystem) return;

        float staminaChange = 0f;

        if (currentState == AIState.Chase || currentState == AIState.IsIt)
        {
            staminaChange = -staminaDepletionRate * deltaTime;
            if (currentStamina <= 0f)
                staminaChange += staminaRecoveryRate * 0.8f * deltaTime;
        }
        else if (currentState == AIState.Retreat)
        {
            staminaChange = -staminaDepletionRate * 1.2f * deltaTime;
        }
        else if (currentState == AIState.Idle || currentState == AIState.Patrol || currentState == AIState.Search)
        {
            staminaChange = staminaRecoveryRate * deltaTime;
        }

        if (isDashing && staminaChange == 0f)
        {
            staminaChange = -dashStaminaCost * deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina + staminaChange, 0f, maxStamina);
    }

    void UpdateFatigueSystem()
    {
        if (!enableFatigueSystem) return;

        float fatigueChange = 0f;

        if (currentState == AIState.Chase || currentState == AIState.IsIt || currentState == AIState.Flanking)
        {
            fatigueChange = fatigueAccumulationRate * deltaTime;
        }

        if ((currentState == AIState.Idle || currentState == AIState.Patrol) && stateTimer > 0.5f)
        {
            fatigueChange = -fatigueRecoveryRate * deltaTime;
        }

        currentFatigue = Mathf.Clamp(currentFatigue + fatigueChange, 0f, maxFatigue);
    }

    void UpdateSafeZoneDetection()
    {
        if (!useSafeZones) return;

        Collider[] safeZones = Physics.OverlapSphere(tf.position, safeZoneDetectionRange, safeZoneLayer);
        
        inSafeZone = safeZones.Length > 0;
        safeZoneCollider = safeZones.Length > 0 ? safeZones[0] : null;

        if (inSafeZone && safeZoneCollider != null && (currentState == AIState.Chase || currentState == AIState.IsIt))
        {
            Vector3 awayDirection = (tf.position - safeZoneCollider.bounds.center).normalized;
            velocity += awayDirection * safeZoneAvoidanceForce * deltaTime;
        }
    }

    void UpdateTagBoostTimer()
    {
        if (tagBoostTimer > 0f)
        {
            tagBoostTimer -= deltaTime;
        }
    }

    void UpdateCooperativeChasing()
    {
        if (currentTime >= nextCooperativeCheck)
        {
            nearbyEnemies.Clear();
            Collider[] nearbyColliders = Physics.OverlapSphere(tf.position, otherEnemyDetectionRange);
            
            foreach (Collider col in nearbyColliders)
            {
                EnemyMovement otherEnemy = col.GetComponent<EnemyMovement>();
                if (otherEnemy != null && otherEnemy != this && otherEnemy.isCurrentlyIt == this.isCurrentlyIt)
                {
                    nearbyEnemies.Add(otherEnemy);
                }
            }

            nextCooperativeCheck = currentTime + 0.5f;
        }
    }

    public void TagPlayer(Transform targetPlayer)
    {
        if (currentTime - lastTagTime < tagCooldown)
        {
            Debug.LogWarning($"[Enemy Movement] Tag failed - on cooldown. Time remaining: {tagCooldown - (currentTime - lastTagTime):F2}s");
            return;
        }
        if (!isCurrentlyIt)
        {
            Debug.LogWarning("[Enemy Movement] Tag failed - this enemy is not currently 'it'");
            return;
        }

        lastTagTime = currentTime;
        tagBoostTimer = tagBoostDuration;

        if (taggedClip)
            audioSource.PlayOneShot(taggedClip, dashVolume);

        Debug.Log($"[Enemy Movement] Successfully tagged {targetPlayer.gameObject.name}! Tag boost active for {tagBoostDuration}s");
    }

    public void BecomeIt()
    {
        isCurrentlyIt = true;
        currentState = AIState.IsIt;
        stateTimer = 0f;

        if (nowItClip)
            audioSource.PlayOneShot(nowItClip, alertVolume);
        
        Debug.Log($"[Enemy Movement] {gameObject.name} is now IT! Tag mode activated.");
    }

    public void StopBeingIt()
    {
        isCurrentlyIt = false;
        currentState = AIState.Idle;
        stateTimer = 0f;
        tagBoostTimer = 0f;
        
        Debug.Log($"[Enemy Movement] {gameObject.name} is no longer IT. Tag mode deactivated.");
    }

    public Transform GetClosestVulnerablePlayer()
    {
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (Transform playerTarget in allPlayers)
        {
            if (playerTarget == null) continue;

            float dist = Vector3.Distance(tf.position, playerTarget.position);
            if (dist < closestDist)
            {
                Collider[] safeZones = Physics.OverlapSphere(playerTarget.position, 1f, safeZoneLayer);
                if (safeZones.Length == 0)
                {
                    closest = playerTarget;
                    closestDist = dist;
                }
            }
        }

        if (closest != null)
        {
            Debug.Log($"[Enemy Movement] Found closest vulnerable player: {closest.gameObject.name} at distance {closestDist:F2}m");
        }
        else
        {
            Debug.LogWarning("[Enemy Movement] No vulnerable players found - all players are in safe zones or unavailable");
        }

        return closest;
    }

    public float GetAdjustedMoveSpeed(float baseSpeed)
    {
        float adjustedSpeed = baseSpeed;
        string adjustmentReasons = "";

        if (enableFatigueSystem && currentFatigue / maxFatigue > fatigueThreshold)
        {
            float fatiguePercent = (currentFatigue / maxFatigue - fatigueThreshold) / (1f - fatigueThreshold);
            adjustedSpeed *= Mathf.Lerp(1f, fatigueSpeedPenalty, fatiguePercent);
            adjustmentReasons += "Fatigue ";
        }

        if (enableStaminaSystem && currentStamina / maxStamina < staminaTiredThreshold)
        {
            float staminaPercent = (currentStamina / maxStamina) / staminaTiredThreshold;
            adjustedSpeed *= Mathf.Max(staminaPercent, 0.2f);
            adjustmentReasons += "Stamina ";
        }

        if (tagBoostTimer > 0f)
        {
            adjustedSpeed *= tagSpeedBoost;
            adjustmentReasons += "TagBoost ";
        }

        if (enableCooperativeChasing && nearbyEnemies.Count > 0)
        {
            adjustedSpeed *= cooperativeBonusAggressiveness;
            adjustmentReasons += "Cooperative";
        }

        if (adjustedSpeed != baseSpeed)
        {
            Debug.Log($"[Enemy Movement] Move speed adjusted from {baseSpeed:F2} to {adjustedSpeed:F2}. Factors: {adjustmentReasons.Trim()}");
        }

        return adjustedSpeed;
    }
    #endregion

    #region Events
    void OnHealthChanged(float currentHealth)
    {
        if (currentState == AIState.Chase && ShouldRetreat())
        {
            TransitionToState(AIState.Retreat, "Health dropped below threshold");
        }

        float healthPercent = enemyHealth != null ? enemyHealth.GetHealthPercentage() : 1f;
        
        stateParameters[AIState.Chase].aggressionLevel = Mathf.Lerp(0.3f, 0.9f, healthPercent);
        stateParameters[AIState.Attack].aggressionLevel = Mathf.Lerp(0.5f, 1.0f, healthPercent);
        stateParameters[AIState.Retreat].urgency = Mathf.Lerp(1.0f, 0.5f, healthPercent);
        
        stateParameters[AIState.Chase].cautionLevel = Mathf.Lerp(0.8f, 0.2f, healthPercent);
        stateParameters[AIState.Flanking].cautionLevel = Mathf.Lerp(0.9f, 0.5f, healthPercent);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;

        PlayerMovement playerMovement = hit.collider.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
            playerMovement.TeleportToSpawn();
    }

    void OnDestroy()
    {
        if (enemyHealth != null && enemyHealth.onHealthChanged != null)
        {
            enemyHealth.onHealthChanged.RemoveListener(OnHealthChanged);
        }
    }

    /// <summary>
    /// Single movement sink. When an AI-controlled PlayerMovement is present it owns the
    /// CharacterController, so this AI only contributes desired velocity/ability input.
    /// </summary>
    private void MoveCharacter(Vector3 delta)
    {
        if (usePlayerMovement) return;
        cc.Move(delta);
    }

    /// <summary>Desired world-space movement produced by this AI for PlayerMovement.</summary>
    public Vector3 DesiredVelocity => velocity;

    /// <summary>True when the AI currently wants a jump/dash/grapple action.</summary>
    public bool WantsJump => currentState == AIState.Attack || isJumping;
    public bool WantsDash => isDashing;

    /// <summary>
    /// Called by the portal momentum receiver after a traversal so the enemy keeps its
    /// own velocity (dash/pursuit momentum) in the destination frame.
    /// </summary>
    public void RotateMomentum(Quaternion rotation)
    {
        velocity = rotation * velocity;
        smoothedPlayerVelocity = rotation * smoothedPlayerVelocity;
        playerVelocity = rotation * playerVelocity;
        if (player != null)
        {
            lastPlayerPos = player.position - (rotation * (player.position - lastPlayerPos));
        }
    }
    #endregion
}