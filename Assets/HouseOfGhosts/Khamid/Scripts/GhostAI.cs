using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GhostTarget))]
[DisallowMultipleComponent]
public class GhostAI : MonoBehaviour
{
    public enum GhostState
    {
        Hidden,
        Stalking,       // Creeping closer to the player to pressure the Danger Meter
        WatchingPlayer, // Behaviour A: Brief pause and stare
        Fleeing,        // Behaviour B or after staring
        Captured
    }

    [Header("Current State (Read Only)")]
    [SerializeField] private GhostState currentState = GhostState.Hidden;
    [SerializeField] private bool isPhasingThroughWall = false;

    [Header("Fake Escape Settings")]
    [Tooltip("Probability (0.5 = 50%) for Behaviour A (stare at player before fleeing).")]
    [Range(0f, 1f)]
    [SerializeField] private float watchPlayerChance = 0.5f;

    [Tooltip("How many seconds the ghost freezes and stares directly at you during Behaviour A.")]
    [SerializeField] private float watchPlayerDuration = 2.0f;

    [Tooltip("If true, losing the flashlight beam after illuminating her during a stare makes her bolt immediately.")]
    [SerializeField] private bool abortStareOnLostBeam = true;

    [Tooltip("Short grace window (seconds) before beam loss cancels her stare.")]
    [SerializeField] private float stareBeamLossGraceSeconds = 0.25f;

    [Header("Movement & Hover Settings")]
    [Tooltip("Ghost forward speed when fleeing towards the next hiding spot (meters/second).")]
    [SerializeField] private float fleeSpeed = 2.8f;

    [Tooltip("Speed of turning when rotating to face you or banking into a dodge.")]
    [SerializeField] private float rotationSpeed = 8.0f;

    [Tooltip("Subtle vertical floating height (meters) while staring, stalking, or fleeing.")]
    [SerializeField] private float hoverAmplitude = 0.08f;

    [Tooltip("Speed of the vertical floating bob.")]
    [SerializeField] private float hoverFrequency = 2.5f;

    [Header("Evasive Weaving (Fleeing)")]
    [Tooltip("If true, the ghost weaves left and right while fleeing instead of moving in a straight line.")]
    [SerializeField] private bool enableEvasiveWeaving = true;

    [Tooltip("How far left/right (in meters) the ghost sways from her core path.")]
    [SerializeField] private float weaveAmplitude = 0.55f;

    [Tooltip("How fast the ghost cycles her left-right weave.")]
    [SerializeField] private float weaveFrequency = 3.0f;

    [Tooltip("Blends predictable Sine wave (0.0) with unpredictable Perlin Noise (1.0). 0.35 feels organic.")]
    [Range(0f, 1f)]
    [SerializeField] private float erraticNoiseBlend = 0.35f;

    [Tooltip("Distance from the destination hiding spot where weaving smoothly tapers down to 0.")]
    [SerializeField] private float arrivalTaperDistance = 1.5f;

    [Tooltip("How quickly her body darts sideways when changing weave/juke direction (meters/sec).")]
    [SerializeField] private float lateralDartResponsiveness = 5.5f;

    [Header("Reactive Flashlight Panic Dodge")]
    [Tooltip("If true, the ghost speeds up, weaves wider, and jukes when hit by the flashlight beam while fleeing.")]
    [SerializeField] private bool enablePanicDodge = true;

    [Tooltip("Forward speed multiplier while the flashlight beam is hitting her during a flee (1.25 = 25% faster).")]
    [SerializeField] private float illuminatedSpeedMultiplier = 1.25f;

    [Tooltip("Weave width multiplier while the flashlight beam is hitting her (1.5 = 50% wider dodges).")]
    [SerializeField] private float illuminatedWeaveMultiplier = 1.5f;

    [Tooltip("Seconds of continuous flashlight illumination while fleeing before she sharply flips dodge direction.")]
    [SerializeField] private float jukeIntervalUnderBeam = 1.0f;

    [Header("Supernatural Wall-Phasing")]
    [Tooltip("If true, the ghost glows spectral cyan, compresses her silhouette, and shimmers when passing through walls.")]
    [SerializeField] private bool enableWallPhasingEffect = true;

    [Tooltip("Radius around the ghost's upper body used to detect walls.")]
    [SerializeField] private float wallDetectRadius = 0.28f;

    [Tooltip("Distance ahead of the ghost to anticipate entering a wall so the glow starts smoothly before contact.")]
    [SerializeField] private float wallLookAheadDistance = 0.35f;

    [Tooltip("How fast the spectral glow and scale distortion fade in and out.")]
    [SerializeField] private float phaseTransitionSpeed = 6.0f;

    [Tooltip("Scale multiplier while squeezing through a solid wall (0.82 width, 1.12 height).")]
    [SerializeField] private Vector3 phaseScaleMultiplier = new Vector3(0.82f, 1.12f, 0.82f);

    [Tooltip("Forward speed multiplier while gliding through a wall (1.1 = 10% smoother glide).")]
    [SerializeField] private float wallPhaseSpeedMultiplier = 1.1f;

    [Tooltip("Fast ethereal shimmer frequency while inside solid wall matter.")]
    [SerializeField] private float phaseShimmerFrequency = 16.0f;

    [Header("UV Ghost Traces (Footprint Trail)")]
    [Tooltip("If true, the ghost leaves behind invisible UV footprint traces along the floor when fleeing or stalking.")]
    [SerializeField] private bool leaveUVTraceTrail = true;

    [Tooltip("Distance (meters) traveled between each dropped UV footprint trace.")]
    [SerializeField] private float traceStepSpacing = 1.0f;

    [Tooltip("How long (seconds) each dropped footprint remains discoverable with the UV flashlight.")]
    [SerializeField] private float traceLifetimeSeconds = 25.0f;

    [Tooltip("Number of reusable UV trace objects in the pool (zero runtime garbage collection).")]
    [SerializeField] private int tracePoolSize = 18;

    [Tooltip("Material using HouseOfGhosts/UVGhostTrace shader for footprints (automatically created if left empty).")]
    [SerializeField] private Material uvTraceMaterial;

    [Header("Stalking / Creeping Behaviour")]
    [Tooltip("If true, when the player takes too long to find the ghost, she creeps toward the player to drain the Danger Meter.")]
    [SerializeField] private bool enableStalking = true;

    [Tooltip("Seconds of hiding without being found before she starts creeping toward you.")]
    [SerializeField] private float stalkStartDelaySeconds = 12.0f;

    [Tooltip("Slow, eerie speed (meters/second) while creeping toward the player.")]
    [SerializeField] private float stalkSpeed = 1.3f;

    [Tooltip("Distance from the player (inside the 4m Danger Meter radius) where she stops creeping and looms.")]
    [SerializeField] private float stalkStopDistance = 3.0f;

    [Tooltip("Angle (degrees) from the center of your view within which looking at a stalking ghost triggers Fake Escape.")]
    [SerializeField] private float stalkSpottedViewAngle = 42.0f;

    [Tooltip("Maximum seconds she will stalk/loom before relocating to a new hiding spot if you never turn around.")]
    [SerializeField] private float maxStalkDurationSeconds = 10.0f;

    [Header("Trigger & Smart Hiding Conditions")]
    [Tooltip("Distance in meters that triggers ghost escape if you approach her hiding spot.")]
    [SerializeField] private float triggerProximityDistance = 2.5f;

    [Tooltip("If true, proximity will NOT trigger through solid walls—you must have line of sight to her.")]
    [SerializeField] private bool requireLineOfSightForProximity = true;

    [Tooltip("Layers that count as walls/obstacles.")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Tooltip("If true, shining the flashlight beam on her while hidden or stalking triggers her Fake Escape.")]
    [SerializeField] private bool triggerOnFlashlightHit = true;

    [Header("References")]
    [Tooltip("Player VR Headset / Camera. Automatically finds Camera.main if left empty.")]
    [SerializeField] private Transform playerHead;

    [Tooltip("List of hiding spots. Automatically finds all GhostHidingSpot objects in the scene if empty.")]
    [SerializeField] private List<GhostHidingSpot> hidingSpots = new List<GhostHidingSpot>();

    private GhostTarget m_GhostTarget;
    private GhostHidingSpot m_CurrentSpot;
    private GhostHidingSpot m_PreviousSpot;
    private GhostHidingSpot m_TargetSpot;

    private float m_WatchTimer = 0f;
    private float m_HiddenTimer = 0f;
    private float m_StalkTimer = 0f;
    private float m_BaseYPosition = 1.0f;

    // Behaviour A beam-loss tracking
    private bool m_WasIlluminatedDuringStare = false;
    private float m_StareBeamLostGraceTimer = 0f;

    // Evasive path tracking state
    private Vector3 m_CorePathPosition;
    private Vector3 m_LastPathForward = Vector3.forward;
    private float m_CurrentLateralOffset = 0f;
    private float m_FleePhaseTime = 0f;
    private float m_TimeUnderBeamWhileFleeing = 0f;
    private float m_JukeSign = 1f;
    private float m_NoiseSeed = 0f;

    // Wall-phasing state
    private Vector3 m_InitialLocalScale = Vector3.one;
    private float m_CurrentWallPhaseBlend = 0f;
    private bool m_WasPhasingLastFrame = false;
    private readonly Collider[] m_WallOverlapBuffer = new Collider[12];

    // UV Footprint Pool state
    private readonly List<GhostTrace> m_TracePool = new List<GhostTrace>();
    private int m_NextTracePoolIndex = 0;
    private Vector3 m_LastTraceDropPosition;
    private bool m_NextFootIsRight = true;
    private Transform m_TracePoolContainer;

    public GhostState CurrentState => currentState;
    public bool IsPhasingThroughWall => isPhasingThroughWall;

    private void Awake()
    {
        m_GhostTarget = GetComponent<GhostTarget>();
        m_BaseYPosition = transform.position.y;
        m_CorePathPosition = transform.position;
        m_InitialLocalScale = transform.localScale;
    }

    private void Start()
    {
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        if (hidingSpots.Count == 0)
        {
            hidingSpots.AddRange(FindObjectsByType<GhostHidingSpot>(FindObjectsSortMode.None));
        }

        if (m_GhostTarget != null)
        {
            m_GhostTarget.onGhostCaptured.AddListener(OnCaptured);
        }

        if (hidingSpots.Count > 0 && m_CurrentSpot == null)
        {
            m_CurrentSpot = hidingSpots[0];
            transform.position = m_CurrentSpot.Position;
            m_CorePathPosition = transform.position;
            m_BaseYPosition = transform.position.y;
        }

        m_LastTraceDropPosition = transform.position;

        if (leaveUVTraceTrail && (m_GhostTarget == null || !m_GhostTarget.IsTestDummy))
        {
            InitializeTracePool();
        }
    }

    private void OnDestroy()
    {
        if (m_GhostTarget != null)
        {
            m_GhostTarget.onGhostCaptured.RemoveListener(OnCaptured);
        }

        if (m_TracePoolContainer != null)
        {
            Destroy(m_TracePoolContainer.gameObject);
        }
    }

    private void Update()
    {
        if (currentState == GhostState.Captured)
            return;

        UpdateWallPhaseState();

        switch (currentState)
        {
            case GhostState.Hidden:
                UpdateHidden();
                break;

            case GhostState.Stalking:
                UpdateStalking();
                break;

            case GhostState.WatchingPlayer:
                UpdateWatchingPlayer();
                break;

            case GhostState.Fleeing:
                UpdateFleeing();
                break;
        }
    }

    private void InitializeTracePool()
    {
        GameObject containerObj = new GameObject($"{gameObject.name}_UVTracePool");
        m_TracePoolContainer = containerObj.transform;

        for (int i = 0; i < tracePoolSize; i++)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"UV_GhostFootprint_{i + 1}";
            quad.transform.SetParent(m_TracePoolContainer, false);
            quad.transform.localScale = new Vector3(0.22f, 0.32f, 1.0f);

            // Remove collider from footprint quad so it never interferes with physics or movement
            Collider quadCollider = quad.GetComponent<Collider>();
            if (quadCollider != null)
            {
                Destroy(quadCollider);
            }

            GhostTrace trace = quad.AddComponent<GhostTrace>();
            if (uvTraceMaterial != null)
            {
                trace.SetTraceMaterial(uvTraceMaterial);
            }
            quad.SetActive(false);
            m_TracePool.Add(trace);
        }
    }

    private void UpdateTraceTrail(Vector3 movementDirection)
    {
        if (!leaveUVTraceTrail || m_TracePool.Count == 0)
            return;

        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 lastXZ = new Vector2(m_LastTraceDropPosition.x, m_LastTraceDropPosition.z);
        float distMoved = Vector2.Distance(currentXZ, lastXZ);

        if (distMoved < traceStepSpacing)
            return;

        m_LastTraceDropPosition = transform.position;

        Vector3 forwardFlat = new Vector3(movementDirection.x, 0f, movementDirection.z);
        if (forwardFlat.sqrMagnitude < 0.001f)
            forwardFlat = m_LastPathForward;
        else
            forwardFlat.Normalize();

        Vector3 rightFlat = Vector3.Cross(Vector3.up, forwardFlat).normalized;
        float footSideSign = m_NextFootIsRight ? 1.0f : -1.0f;
        m_NextFootIsRight = !m_NextFootIsRight;

        Vector3 stepWorldPos = transform.position + (rightFlat * (0.11f * footSideSign));

        // Find exact floor surface below the ghost so the print sits 1.5cm above the floor
        float floorY = FindFloorHeightAt(stepWorldPos);
        stepWorldPos.y = floorY + 0.015f;

        // Rotate quad flat onto the floor (90 deg X) pointing along the ghost's travel direction
        Quaternion stepRotation = Quaternion.LookRotation(forwardFlat, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

        GhostTrace pooledTrace = m_TracePool[m_NextTracePoolIndex];
        m_NextTracePoolIndex = (m_NextTracePoolIndex + 1) % m_TracePool.Count;

        if (pooledTrace != null)
        {
            pooledTrace.ActivateAt(stepWorldPos, stepRotation, traceLifetimeSeconds);
        }
    }

    private float FindFloorHeightAt(Vector3 worldPos)
    {
        Vector3 rayStart = new Vector3(worldPos.x, transform.position.y + 0.5f, worldPos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 4.0f, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                return hit.point.y;
            }
        }

        // Fallback assuming capsule center is ~1.0m above the floor
        return m_BaseYPosition - 0.98f;
    }

    private void UpdateWallPhaseState()
    {
        if (!enableWallPhasingEffect)
        {
            isPhasingThroughWall = false;
            m_CurrentWallPhaseBlend = 0f;
            transform.localScale = m_InitialLocalScale;
            if (m_GhostTarget != null)
                m_GhostTarget.SetWallPhaseBlend(0f);
            return;
        }

        bool checkLookAhead = (currentState == GhostState.Fleeing || currentState == GhostState.Stalking);
        isPhasingThroughWall = DetectWallOverlap(checkLookAhead);

        if (isPhasingThroughWall && !m_WasPhasingLastFrame)
        {
            Debug.Log($"[{gameObject.name}] Phasing through solid wall (Spectral Shift active)!");
        }
        m_WasPhasingLastFrame = isPhasingThroughWall;

        float targetBlend = isPhasingThroughWall ? 1.0f : 0.0f;
        m_CurrentWallPhaseBlend = Mathf.MoveTowards(m_CurrentWallPhaseBlend, targetBlend, phaseTransitionSpeed * Time.deltaTime);

        if (m_GhostTarget != null)
        {
            m_GhostTarget.SetWallPhaseBlend(m_CurrentWallPhaseBlend);
        }

        transform.localScale = new Vector3(
            m_InitialLocalScale.x * Mathf.Lerp(1f, phaseScaleMultiplier.x, m_CurrentWallPhaseBlend),
            m_InitialLocalScale.y * Mathf.Lerp(1f, phaseScaleMultiplier.y, m_CurrentWallPhaseBlend),
            m_InitialLocalScale.z * Mathf.Lerp(1f, phaseScaleMultiplier.z, m_CurrentWallPhaseBlend)
        );
    }

    private bool DetectWallOverlap(bool includeForwardLookAhead)
    {
        Vector3 center = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        Vector3 bottomPoint = center - Vector3.up * 0.15f;
        Vector3 topPoint = center + Vector3.up * 0.45f;

        int count = Physics.OverlapCapsuleNonAlloc(
            bottomPoint,
            topPoint,
            wallDetectRadius,
            m_WallOverlapBuffer,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        if (HasValidWallCollider(count, center.y))
            return true;

        if (includeForwardLookAhead && wallLookAheadDistance > 0.01f)
        {
            Vector3 lookAheadCenter = center + (m_LastPathForward * wallLookAheadDistance) + Vector3.up * 0.15f;
            int aheadCount = Physics.OverlapSphereNonAlloc(
                lookAheadCenter,
                wallDetectRadius * 0.85f,
                m_WallOverlapBuffer,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            );

            if (HasValidWallCollider(aheadCount, center.y))
                return true;
        }

        return false;
    }

    private bool HasValidWallCollider(int hitCount, float ghostCenterY)
    {
        float floorHeightCutoff = ghostCenterY - 0.45f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_WallOverlapBuffer[i];
            if (col == null)
                continue;

            if (col.transform.IsChildOf(transform))
                continue;

            if (playerHead != null && col.transform.IsChildOf(playerHead.root))
                continue;

            if (col.GetComponentInParent<GhostTarget>() != null)
                continue;

            if (col.bounds.max.y <= floorHeightCutoff)
                continue;

            return true;
        }

        return false;
    }

    private void UpdateHidden()
    {
        if (triggerOnFlashlightHit && m_GhostTarget != null && m_GhostTarget.IsIlluminated)
        {
            TriggerFakeEscape("Flashlight beam spotted the ghost!");
            return;
        }

        if (playerHead != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerHead.position);
            if (distanceToPlayer <= triggerProximityDistance)
            {
                if (!requireLineOfSightForProximity || HasLineOfSightToPlayer())
                {
                    TriggerFakeEscape("Player spotted the ghost at close range!");
                    return;
                }
            }
        }

        if (enableStalking && playerHead != null)
        {
            m_HiddenTimer += Time.deltaTime;
            if (m_HiddenTimer >= stalkStartDelaySeconds)
            {
                m_HiddenTimer = 0f;
                StartStalking();
            }
        }
    }

    private void StartStalking()
    {
        currentState = GhostState.Stalking;
        m_StalkTimer = 0f;
        m_BaseYPosition = transform.position.y;
        m_LastTraceDropPosition = transform.position;
        Debug.Log($"[{gameObject.name}] Player hasn't found ghost for {stalkStartDelaySeconds:F0}s -> Entering STALKING state (creeping closer)!");
    }

    private void UpdateStalking()
    {
        if (triggerOnFlashlightHit && m_GhostTarget != null && m_GhostTarget.IsIlluminated)
        {
            TriggerFakeEscape("Flashlight caught the ghost stalking!");
            return;
        }

        if (playerHead == null)
        {
            StartFleeing();
            return;
        }

        if (IsPlayerLookingAtGhost() && HasLineOfSightToPlayer())
        {
            TriggerFakeEscape("Player turned around and caught the ghost stalking!");
            return;
        }

        Vector3 flatPlayerPos = new Vector3(playerHead.position.x, m_BaseYPosition, playerHead.position.z);
        Vector3 currentFlatPos = new Vector3(transform.position.x, m_BaseYPosition, transform.position.z);
        Vector3 toPlayer = flatPlayerPos - currentFlatPos;
        float distanceToPlayer = toPlayer.magnitude;

        if (toPlayer.sqrMagnitude > 0.001f)
        {
            m_LastPathForward = toPlayer.normalized;
            Quaternion lookRot = Quaternion.LookRotation(m_LastPathForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
        }

        if (distanceToPlayer > stalkStopDistance)
        {
            float activeStalkSpeed = stalkSpeed * Mathf.Lerp(1f, wallPhaseSpeedMultiplier, m_CurrentWallPhaseBlend);
            Vector3 nextFlatPos = Vector3.MoveTowards(currentFlatPos, flatPlayerPos, activeStalkSpeed * Time.deltaTime);
            float hoverY = m_BaseYPosition + CalculateHoverAndShimmerOffset();
            transform.position = new Vector3(nextFlatPos.x, hoverY, nextFlatPos.z);

            UpdateTraceTrail(m_LastPathForward);
        }
        else
        {
            ApplyHoverOffset(m_BaseYPosition);
        }

        m_StalkTimer += Time.deltaTime;
        if (m_StalkTimer >= maxStalkDurationSeconds)
        {
            Debug.Log($"[{gameObject.name}] Stalk duration finished — retreating to a tactical hiding spot.");
            StartFleeing();
        }
    }

    private bool IsPlayerLookingAtGhost()
    {
        if (playerHead == null)
            return false;

        Vector3 targetCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        Vector3 toGhost = targetCenter - playerHead.position;
        float distance = toGhost.magnitude;

        if (distance < 0.01f)
            return true;

        float angle = Vector3.Angle(playerHead.forward, toGhost / distance);
        return angle <= stalkSpottedViewAngle;
    }

    private bool HasLineOfSightToPlayer()
    {
        return HasLineOfSightFromPlayerToPoint(m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position);
    }

    private bool HasLineOfSightFromPlayerToPoint(Vector3 worldPoint)
    {
        if (playerHead == null)
            return true;

        Vector3 origin = playerHead.position;
        Vector3 toPoint = worldPoint - origin;
        float distance = toPoint.magnitude;

        if (distance < 0.2f)
            return true;

        if (Physics.Raycast(origin, toPoint / distance, out RaycastHit hit, distance, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(playerHead.root))
                return true;

            return false;
        }

        return true;
    }

    private void UpdateWatchingPlayer()
    {
        if (playerHead != null)
        {
            Vector3 directionToPlayer = playerHead.position - transform.position;
            directionToPlayer.y = 0f;

            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        ApplyHoverOffset(m_BaseYPosition);

        bool isIlluminated = m_GhostTarget != null && m_GhostTarget.IsIlluminated;
        if (isIlluminated)
        {
            m_WasIlluminatedDuringStare = true;
            m_StareBeamLostGraceTimer = stareBeamLossGraceSeconds;
        }
        else if (m_WasIlluminatedDuringStare && abortStareOnLostBeam)
        {
            m_StareBeamLostGraceTimer -= Time.deltaTime;
            if (m_StareBeamLostGraceTimer <= 0f)
            {
                Debug.Log($"[{gameObject.name}] Player lost flashlight beam during Behaviour A stare! Immediately bolting!");
                StartFleeing();
                return;
            }
        }

        m_WatchTimer -= Time.deltaTime;
        if (m_WatchTimer <= 0f)
        {
            Debug.Log($"[{gameObject.name}] Behaviour A stare finished! Now fleeing...");
            StartFleeing();
        }
    }

    private void UpdateFleeing()
    {
        if (m_TargetSpot == null)
        {
            currentState = GhostState.Hidden;
            return;
        }

        bool isIlluminated = m_GhostTarget != null && m_GhostTarget.IsIlluminated;

        // 1. Reactive Flashlight Panic & Juke Check
        float speedMultiplier = Mathf.Lerp(1f, wallPhaseSpeedMultiplier, m_CurrentWallPhaseBlend);
        float weaveMultiplier = 1.0f;

        if (enablePanicDodge && isIlluminated)
        {
            speedMultiplier *= illuminatedSpeedMultiplier;
            weaveMultiplier = illuminatedWeaveMultiplier;

            m_TimeUnderBeamWhileFleeing += Time.deltaTime;
            if (m_TimeUnderBeamWhileFleeing >= jukeIntervalUnderBeam)
            {
                m_TimeUnderBeamWhileFleeing = 0f;
                m_JukeSign *= -1f;
                Debug.Log($"[{gameObject.name}] Panic Juke! Ghost flipped dodge direction to escape the flashlight beam!");
            }
        }
        else
        {
            m_TimeUnderBeamWhileFleeing = Mathf.Max(0f, m_TimeUnderBeamWhileFleeing - Time.deltaTime * 2.0f);
        }

        // 2. Advance Invisible Core Path Position
        Vector3 targetPos = m_TargetSpot.Position;
        Vector3 toTarget = targetPos - m_CorePathPosition;
        float remainingDistance = toTarget.magnitude;

        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flatToTarget.sqrMagnitude > 0.0001f)
        {
            m_LastPathForward = flatToTarget.normalized;
        }

        float currentSpeed = fleeSpeed * speedMultiplier;
        m_CorePathPosition = Vector3.MoveTowards(m_CorePathPosition, targetPos, currentSpeed * Time.deltaTime);

        // 3. Calculate Organic Lateral Weave Offset
        float desiredLateralOffset = 0f;
        if (enableEvasiveWeaving)
        {
            m_FleePhaseTime += Time.deltaTime * weaveFrequency * weaveMultiplier;

            float startTaper = Mathf.Clamp01(m_FleePhaseTime / 1.0f);
            float endTaper = arrivalTaperDistance > 0.01f
                ? Mathf.Clamp01(remainingDistance / arrivalTaperDistance)
                : 1.0f;

            float wallPassDamping = 1.0f - (m_CurrentWallPhaseBlend * 0.75f);
            float envelope = startTaper * endTaper * wallPassDamping;

            float sineWave = Mathf.Sin(m_FleePhaseTime) * m_JukeSign;
            float perlinWave = (Mathf.PerlinNoise(m_NoiseSeed, m_FleePhaseTime * 0.6f) - 0.5f) * 2.0f;
            float combinedWave = Mathf.Lerp(sineWave, perlinWave, erraticNoiseBlend);

            desiredLateralOffset = combinedWave * weaveAmplitude * weaveMultiplier * envelope;
        }

        m_CurrentLateralOffset = Mathf.MoveTowards(
            m_CurrentLateralOffset,
            desiredLateralOffset,
            lateralDartResponsiveness * weaveMultiplier * Time.deltaTime
        );

        // 4. Combine Core Position + Perpendicular Lateral Offset + Vertical Hover & Wall Shimmer
        Vector3 pathRight = Vector3.Cross(Vector3.up, m_LastPathForward).normalized;
        float verticalOffset = CalculateHoverAndShimmerOffset();

        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = m_CorePathPosition + (pathRight * m_CurrentLateralOffset) + (Vector3.up * verticalOffset);
        transform.position = nextPosition;

        // 5. Rotate Smoothly Into the Actual Curved Movement Direction & Drop UV Footprints
        Vector3 actualVelocityDir = nextPosition - previousPosition;
        actualVelocityDir.y = 0f;
        if (actualVelocityDir.sqrMagnitude > 0.00001f)
        {
            Vector3 moveNormal = actualVelocityDir.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(moveNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            UpdateTraceTrail(moveNormal);
        }
        else
        {
            UpdateTraceTrail(m_LastPathForward);
        }

        // 6. Check Arrival at Hiding Spot
        if (Vector3.Distance(m_CorePathPosition, targetPos) <= 0.1f)
        {
            m_PreviousSpot = m_CurrentSpot;
            m_CurrentSpot = m_TargetSpot;
            m_CorePathPosition = m_CurrentSpot.Position;
            transform.position = m_CurrentSpot.Position;
            m_BaseYPosition = m_CurrentSpot.Position.y;
            m_CurrentLateralOffset = 0f;
            m_HiddenTimer = 0f;
            currentState = GhostState.Hidden;
            Debug.Log($"[{gameObject.name}] Ghost safely reached tactical hiding spot: {m_CurrentSpot.SpotName}. Hidden again.");
        }
    }

    private float CalculateHoverAndShimmerOffset()
    {
        float normalHover = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        if (m_CurrentWallPhaseBlend <= 0.001f)
            return normalHover;

        float phaseShimmer = Mathf.Sin(Time.time * phaseShimmerFrequency) * (hoverAmplitude * 0.45f) * m_CurrentWallPhaseBlend;
        return normalHover + phaseShimmer;
    }

    private void ApplyHoverOffset(float baseY)
    {
        float hoverY = baseY + CalculateHoverAndShimmerOffset();
        transform.position = new Vector3(transform.position.x, hoverY, transform.position.z);
    }

    [ContextMenu("Trigger Fake Escape")]
    public void TriggerFakeEscape(string reason = "Triggered")
    {
        if (currentState != GhostState.Hidden && currentState != GhostState.Stalking)
            return;

        m_HiddenTimer = 0f;
        m_StalkTimer = 0f;
        m_BaseYPosition = transform.position.y;
        bool chooseWatch = Random.value <= watchPlayerChance;

        if (chooseWatch)
        {
            currentState = GhostState.WatchingPlayer;
            m_WatchTimer = watchPlayerDuration;
            m_WasIlluminatedDuringStare = false;
            m_StareBeamLostGraceTimer = stareBeamLossGraceSeconds;
            Debug.Log($"[{gameObject.name}] {reason} -> Fake Escape [Behaviour A: Staring at player for {watchPlayerDuration:F1}s]");
        }
        else
        {
            Debug.Log($"[{gameObject.name}] {reason} -> Fake Escape [Behaviour B: Immediate Escape!]");
            StartFleeing();
        }
    }

    public void StartNewRound(
        float newFleeSpeed,
        float newWatchDuration,
        float newWatchChance,
        float newWeaveAmplitude = -1f,
        float newWeaveFrequency = -1f)
    {
        if (m_GhostTarget == null)
        {
            m_GhostTarget = GetComponent<GhostTarget>();
        }

        fleeSpeed = newFleeSpeed;
        watchPlayerDuration = newWatchDuration;
        watchPlayerChance = Mathf.Clamp01(newWatchChance);

        if (newWeaveAmplitude >= 0f)
            weaveAmplitude = newWeaveAmplitude;

        if (newWeaveFrequency >= 0f)
            weaveFrequency = newWeaveFrequency;

        m_HiddenTimer = 0f;
        m_StalkTimer = 0f;
        m_CurrentLateralOffset = 0f;
        m_CurrentWallPhaseBlend = 0f;
        m_WasIlluminatedDuringStare = false;
        transform.localScale = m_InitialLocalScale;

        GhostHidingSpot spawnSpot = GetSmartNextHidingSpot();
        if (spawnSpot != null)
        {
            m_PreviousSpot = m_CurrentSpot;
            m_CurrentSpot = spawnSpot;
            m_TargetSpot = null;
            m_CorePathPosition = m_CurrentSpot.Position;
            transform.position = m_CurrentSpot.Position;
            m_BaseYPosition = m_CurrentSpot.Position.y;
            m_LastTraceDropPosition = transform.position;
        }

        currentState = GhostState.Hidden;

        if (m_GhostTarget != null)
        {
            m_GhostTarget.ResetTarget();
        }
        else
        {
            gameObject.SetActive(true);
        }

        Debug.Log($"[{gameObject.name}] Respawned at '{(m_CurrentSpot != null ? m_CurrentSpot.SpotName : "Start")}' | Speed: {fleeSpeed:F2} m/s | Weave: {weaveAmplitude:F2}m @ {weaveFrequency:F1}Hz | Stare: {watchPlayerDuration:F1}s");
    }

    private void StartFleeing()
    {
        m_TargetSpot = GetSmartNextHidingSpot();
        m_CorePathPosition = transform.position;
        m_LastTraceDropPosition = transform.position;
        m_CurrentLateralOffset = 0f;
        m_FleePhaseTime = 0f;
        m_TimeUnderBeamWhileFleeing = 0f;
        m_JukeSign = Random.value > 0.5f ? 1f : -1f;
        m_NoiseSeed = Random.Range(0f, 100f);

        if (m_TargetSpot != null)
        {
            Vector3 flatDir = m_TargetSpot.Position - m_CorePathPosition;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                m_LastPathForward = flatDir.normalized;
            }
            Debug.Log($"[{gameObject.name}] Ghost fleeing to tactical spot: {m_TargetSpot.SpotName}");
        }

        // Drop a first UV trace right where she bolted from!
        UpdateTraceTrail(m_LastPathForward);
        currentState = GhostState.Fleeing;
    }

    private GhostHidingSpot GetSmartNextHidingSpot()
    {
        if (hidingSpots.Count == 0)
            return null;

        if (hidingSpots.Count == 1)
            return hidingSpots[0];

        GhostHidingSpot bestSpot = null;
        float bestScore = float.MinValue;

        Vector3 playerPos = playerHead != null ? playerHead.position : transform.position;
        Vector3 playerForward = playerHead != null ? playerHead.forward : Vector3.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude > 0.001f)
            playerForward.Normalize();

        foreach (GhostHidingSpot spot in hidingSpots)
        {
            if (spot == null || spot == m_CurrentSpot)
                continue;

            float score = 0f;
            Vector3 spotPos = spot.Position;
            Vector3 toSpot = spotPos - playerPos;
            float distToPlayer = toSpot.magnitude;

            bool playerCanSeeSpot = HasLineOfSightFromPlayerToPoint(spotPos + Vector3.up * 0.5f);
            if (!playerCanSeeSpot)
            {
                score += 40f;
            }

            Vector3 flatToSpot = new Vector3(toSpot.x, 0f, toSpot.z);
            if (flatToSpot.sqrMagnitude > 0.001f)
            {
                float gazeDot = Vector3.Dot(playerForward, flatToSpot.normalized);
                if (gazeDot < 0f)
                {
                    score += 25f;
                }
                else if (gazeDot > 0.65f)
                {
                    score -= 40f;
                }
            }

            if (distToPlayer > triggerProximityDistance + 1.0f)
            {
                score += 20f;
            }
            else if (distToPlayer <= triggerProximityDistance)
            {
                score -= 50f;
            }

            if (spot == m_PreviousSpot && hidingSpots.Count > 2)
            {
                score -= 35f;
            }

            score += Random.Range(0f, 12f);

            if (score > bestScore)
            {
                bestScore = score;
                bestSpot = spot;
            }
        }

        return bestSpot != null ? bestSpot : m_CurrentSpot;
    }

    private void OnCaptured()
    {
        if (m_GhostTarget != null && m_GhostTarget.IsTestDummy)
            return;

        currentState = GhostState.Captured;
        transform.localScale = m_InitialLocalScale;
        Debug.Log($"[{gameObject.name}] Ghost captured! AI stopped.");
    }

    private void OnDrawGizmosSelected()
    {
        if (isPhasingThroughWall)
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.55f);
        }

        if (currentState == GhostState.Fleeing && m_TargetSpot != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(m_CorePathPosition, m_TargetSpot.Position);
            Gizmos.DrawWireSphere(m_CorePathPosition, 0.15f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(m_CorePathPosition, transform.position);
        }
        else if (currentState == GhostState.Stalking && playerHead != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, playerHead.position);
        }
    }
}
