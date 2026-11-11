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
    [SerializeField] private float currentSpectralVisibility = 0f;

    [Header("Stage 1: Cloak & Supernatural Materialization")]
    [Tooltip("If true, the ghost is completely invisible while Hidden and materializes smoothly when discovered.")]
    [SerializeField] private bool cloakWhileHidden = true;

    [Tooltip("Seconds it takes for the ghost to materialize from floor to head (0% -> 100%) when spotted.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float materializeDuration = 0.45f;

    [Tooltip("Seconds it takes for the ghost to melt into shadows (100% -> 0%) after reaching a hiding spot.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float dematerializeDuration = 0.40f;

    [Tooltip("If true, the ghost CANNOT turn invisible at a hiding spot while your Normal flashlight beam is actively hitting her.")]
    [SerializeField] private bool preventCloakWhileIlluminated = true;

    [Tooltip("Semi-translucent phantom visibility (0.62 = 62%) while creeping toward the player in Stalking state.")]
    [Range(0.2f, 1.0f)]
    [SerializeField] private float stalkPhantomVisibility = 0.62f;

    [Tooltip("Ghostly UV silhouette visibility (0.38 = 38%) when you shine the UV flashlight onto a cloaked Hidden ghost.")]
    [Range(0.15f, 0.85f)]
    [SerializeField] private float uvCloakShimmerVisibility = 0.38f;

    [Tooltip("Rim glow color when the UV beam reveals a cloaked ghost.")]
    [SerializeField] private Color uvShimmerRimColor = new Color(0.58f, 0.22f, 1.0f, 1.0f);

    [Tooltip("If true, holding the UV beam on a Hidden ghost for uvDisturbDuration spooks her into Fake Escape.")]
    [SerializeField] private bool disturbWithUVBeam = true;

    [Tooltip("Seconds of continuous UV beam exposure before a Hidden ghost bolts (gives you time to switch to Normal light!).")]
    [SerializeField] private float uvDisturbDuration = 1.2f;

    [Header("Stage 2: Flashlight Beam-Lock Struggle")]
    [Tooltip("If true, holding the Normal beam on the ghost makes her visibly tremble and drags down her flee speed as capture nears 3s.")]
    [SerializeField] private bool enableBeamStruggle = true;

    [Tooltip("Maximum flee speed reduction (0.45 = 45% slower) as the 3-second capture progress nears 100%.")]
    [Range(0f, 0.8f)]
    [SerializeField] private float maxBeamSlowdownFactor = 0.45f;

    [Tooltip("Subtle struggle shiver intensity (meters) as capture nears 3s (0.02 = gentle supernatural tremble).")]
    [Range(0f, 0.08f)]
    [SerializeField] private float struggleTrembleAmplitude = 0.02f;

    [Tooltip("Vibration speed (Hz) while struggling near the end of the 3s capture.")]
    [SerializeField] private float struggleTrembleFrequency = 26.0f;

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

    [Header("Evasive Weaving (Smooth Ghostly Glide)")]
    [Tooltip("If true, the ghost glides in a smooth S-curve while fleeing instead of a robotic straight line.")]
    [SerializeField] private bool enableEvasiveWeaving = true;

    [Tooltip("How far left/right (meters) the ghost gently drifts from her core path (0.30m = smooth ghostly sway).")]
    [SerializeField] private float weaveAmplitude = 0.30f;

    [Tooltip("Cycles per second of her left-right glide (0.55 Hz = ~1.8s per full smooth S-curve).")]
    [SerializeField] private float weaveFrequency = 0.55f;

    [Tooltip("Higher = softer, floatier ghostly curves (seconds). Lower = sharper turns.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float weaveSmoothTime = 0.42f;

    [Tooltip("Blends pure Sine wave (0.0) with subtle organic variation (0.12 feels smooth yet alive).")]
    [Range(0f, 1f)]
    [SerializeField] private float erraticNoiseBlend = 0.12f;

    [Tooltip("Distance from the destination hiding spot where weaving smoothly tapers down to 0.")]
    [SerializeField] private float arrivalTaperDistance = 1.6f;

    [Header("Reactive Flashlight Panic Dodge")]
    [Tooltip("If true, the ghost subtly surges and curves away when hit by the flashlight beam while fleeing.")]
    [SerializeField] private bool enablePanicDodge = true;

    [Tooltip("Initial startle speed multiplier when first hit by the beam (overcome by Stage 2 Beam-Lock drag as capture fills!).")]
    [SerializeField] private float illuminatedSpeedMultiplier = 1.15f;

    [Tooltip("Subtle weave width multiplier while the flashlight beam is hitting her (1.2 = 20% wider smooth arc).")]
    [SerializeField] private float illuminatedWeaveMultiplier = 1.20f;

    [Tooltip("Seconds of continuous flashlight illumination while fleeing before she smoothly curves to the opposite side.")]
    [SerializeField] private float jukeIntervalUnderBeam = 2.2f;

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

    [Header("Stage 3: UV Footprints, Wall-Phase Rifts & Escape Clues")]
    [Tooltip("If true, the ghost leaves behind invisible UV footprint traces along the floor when fleeing or stalking.")]
    [SerializeField] private bool leaveUVTraceTrail = true;

    [Tooltip("Distance (meters) traveled between each dropped UV footprint trace.")]
    [SerializeField] private float traceStepSpacing = 1.0f;

    [Tooltip("How long (seconds) each dropped footprint remains discoverable with the UV flashlight.")]
    [SerializeField] private float traceLifetimeSeconds = 25.0f;

    [Tooltip("Fluorescent color of standard UV footprints.")]
    [SerializeField] private Color footprintUVColor = new Color(0.15f, 1.0f, 0.78f, 1.0f);

    [Tooltip("If true, passing through a solid wall leaves glowing UV Spectral Rift marks on BOTH sides of the wall!")]
    [SerializeField] private bool leaveWallPhaseUVMarks = true;

    [Tooltip("Diameter (meters) of the UV Spectral Rift mark left on walls.")]
    [SerializeField] private float wallMarkSize = 0.55f;

    [Tooltip("Seconds that UV Wall-Phase Rift marks stay discoverable.")]
    [SerializeField] private float wallMarkLifetimeSeconds = 32.0f;

    [Tooltip("Fluorescent cyan-violet color for UV Wall-Phase Rift marks.")]
    [SerializeField] private Color wallMarkUVColor = new Color(0.38f, 0.72f, 1.0f, 1.0f);

    [Tooltip("If true, bolting from a hiding spot leaves a pulsing UV Residual Ring at the spot she escaped from.")]
    [SerializeField] private bool leaveEscapeOriginMark = true;

    [Tooltip("Number of reusable UV trace objects in the pool (zero runtime garbage collection).")]
    [SerializeField] private int tracePoolSize = 24;

    [Tooltip("Material using HouseOfGhosts/UVGhostTrace shader for UV traces.")]
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

    [Header("Stage 5: Search Hint Visual Settings")]
    [Tooltip("Duration (seconds) of the brief ghostly silhouette flicker when RoundManager triggers a 60s Search Hint.")]
    [SerializeField] private float hintFlickerDuration = 1.6f;

    [Tooltip("Number of UV breadcrumb rings spawned toward the ghost when a Search Hint triggers.")]
    [Range(0, 8)]
    [SerializeField] private int hintBreadcrumbCount = 4;

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

    // Stage 1 cloak & UV disturb state
    private float m_UVExposureTimer = 0f;
    private float m_HintShimmerTimer = 0f;
    private bool m_IsUsingHeightWipe = false;

    // Behaviour A beam-loss tracking
    private bool m_WasIlluminatedDuringStare = false;
    private float m_StareBeamLostGraceTimer = 0f;

    // Evasive path tracking state (SmoothDamp floating curves)
    private Vector3 m_CorePathPosition;
    private Vector3 m_LastPathForward = Vector3.forward;
    private float m_CurrentLateralOffset = 0f;
    private float m_LateralOffsetVelocity = 0f;
    private float m_FleePhaseTime = 0f;
    private float m_FleeElapsedSeconds = 0f;
    private float m_TimeUnderBeamWhileFleeing = 0f;
    private float m_JukeSign = 1f;
    private float m_SmoothedJukeSign = 1f;
    private float m_JukeSignVelocity = 0f;
    private float m_NoiseSeed = 0f;

    // Wall-phasing & Stage 3 wall-mark state
    private Vector3 m_InitialLocalScale = Vector3.one;
    private float m_CurrentWallPhaseBlend = 0f;
    private bool m_WasPhasingLastFrame = false;
    private float m_LastWallMarkTime = -10f;
    private readonly Collider[] m_WallOverlapBuffer = new Collider[12];

    // UV Trace Pool state
    private readonly List<GhostTrace> m_TracePool = new List<GhostTrace>();
    private int m_NextTracePoolIndex = 0;
    private Vector3 m_LastTraceDropPosition;
    private bool m_NextFootIsRight = true;
    private Transform m_TracePoolContainer;

    private static readonly Color s_PhantomRimColor = new Color(0.32f, 0.86f, 1.0f, 1.0f);

    public GhostState CurrentState => currentState;
    public bool IsPhasingThroughWall => isPhasingThroughWall;
    public float CurrentSpectralVisibility => currentSpectralVisibility;

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

        if (m_GhostTarget == null || !m_GhostTarget.IsTestDummy)
        {
            InitializeTracePool();
        }

        // Start cloaked if starting in Hidden state
        currentSpectralVisibility = (cloakWhileHidden && currentState == GhostState.Hidden) ? 0f : 1f;
        PushSpectralStateToTarget(currentSpectralVisibility, false, s_PhantomRimColor, 0f);
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
        UpdateCloakAndSpectralPresence();

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

    // =========================================================================
    // STAGE 1: CLOAK, BOTTOM-UP MATERIALIZE, UV SHIMMER & STALKING PHANTOM
    // =========================================================================
    private void UpdateCloakAndSpectralPresence()
    {
        if (!cloakWhileHidden)
        {
            currentSpectralVisibility = 1.0f;
            PushSpectralStateToTarget(1.0f, false, s_PhantomRimColor, 0f);
            return;
        }

        if (m_HintShimmerTimer > 0f)
        {
            m_HintShimmerTimer -= Time.deltaTime;
        }

        float targetVisibility = 1.0f;
        float transitionSpeed = 1.0f / Mathf.Max(0.08f, materializeDuration);
        Color rimColor = s_PhantomRimColor;
        float rimIntensity = 0f;

        bool hitByNormalBeam = m_GhostTarget != null && m_GhostTarget.IsIlluminated;
        bool hitByUVBeam = m_GhostTarget != null && m_GhostTarget.IsUVIlluminated;

        if (currentState == GhostState.Hidden)
        {
            if (hitByUVBeam)
            {
                // UV Cloak Shimmer: Eerie ultraviolet/cyan silhouette while Hidden!
                float uvPulse = 0.88f + 0.12f * Mathf.Sin(Time.time * 9.0f);
                targetVisibility = uvCloakShimmerVisibility * uvPulse;
                m_IsUsingHeightWipe = false;
                transitionSpeed = 4.5f;
                rimColor = uvShimmerRimColor;
                rimIntensity = 1.85f;
            }
            else if (m_HintShimmerTimer > 0f)
            {
                // Stage 5: Brief 60s Search Hint spectral apparition pulse
                float hintWave = Mathf.Sin((1.0f - (m_HintShimmerTimer / Mathf.Max(0.1f, hintFlickerDuration))) * Mathf.PI);
                targetVisibility = 0.48f * Mathf.Clamp01(hintWave * 1.3f);
                m_IsUsingHeightWipe = false;
                transitionSpeed = 3.5f;
                rimColor = s_PhantomRimColor;
                rimIntensity = 1.6f;
            }
            else
            {
                // Dematerialize into complete invisibility (0%)
                targetVisibility = 0.0f;
                transitionSpeed = 1.0f / Mathf.Max(0.08f, dematerializeDuration);
                if (currentSpectralVisibility > 0.42f)
                {
                    m_IsUsingHeightWipe = true;
                }
            }
        }
        else if (currentState == GhostState.Stalking)
        {
            // Stalking Phantom Apparition: Semi-ethereal flickering ghostly form
            float eerieFlicker = (Mathf.PerlinNoise(Time.time * 3.8f, m_NoiseSeed) - 0.5f) * 0.14f;
            targetVisibility = Mathf.Clamp01(stalkPhantomVisibility + eerieFlicker);
            m_IsUsingHeightWipe = false;
            transitionSpeed = 2.2f;
            rimColor = s_PhantomRimColor;
            rimIntensity = 0.95f;
        }
        else
        {
            // WatchingPlayer or Fleeing -> Full Bottom-Up Supernatural Materialization to 100%!
            targetVisibility = 1.0f;
            transitionSpeed = 1.0f / Mathf.Max(0.08f, materializeDuration);
            if (currentSpectralVisibility < 0.98f)
            {
                m_IsUsingHeightWipe = true;
                rimIntensity = (1.0f - currentSpectralVisibility) * 1.4f;
            }
            else
            {
                m_IsUsingHeightWipe = false;
            }
        }

        // Anti-Frustration Beam Burn-Lock: Normal beam burns away any cloak!
        if (hitByNormalBeam && currentState != GhostState.Hidden)
        {
            targetVisibility = 1.0f;
        }

        currentSpectralVisibility = Mathf.MoveTowards(currentSpectralVisibility, targetVisibility, transitionSpeed * Time.deltaTime);
        PushSpectralStateToTarget(currentSpectralVisibility, m_IsUsingHeightWipe, rimColor, rimIntensity);
    }

    private void PushSpectralStateToTarget(float visibility, bool useWipe, Color rimColor, float rimIntensity)
    {
        if (m_GhostTarget != null)
        {
            m_GhostTarget.SetSpectralState(visibility, useWipe, rimColor, rimIntensity);
        }
    }

    // =========================================================================
    // STAGE 3: UV TRACE POOL (FOOTPRINTS, WALL-PHASE RIFTS & RESIDUAL RINGS)
    // =========================================================================
    private void InitializeTracePool()
    {
        if (m_TracePool.Count > 0)
            return;

        GameObject containerObj = new GameObject($"{gameObject.name}_UVTracePool");
        m_TracePoolContainer = containerObj.transform;

        int poolCount = Mathf.Max(8, tracePoolSize);
        for (int i = 0; i < poolCount; i++)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"UV_GhostTrace_{i + 1}";
            quad.transform.SetParent(m_TracePoolContainer, false);
            quad.transform.localScale = new Vector3(0.22f, 0.32f, 1.0f);

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

    private GhostTrace GetNextPooledTrace()
    {
        if (m_TracePool.Count == 0)
            return null;

        GhostTrace trace = m_TracePool[m_NextTracePoolIndex];
        m_NextTracePoolIndex = (m_NextTracePoolIndex + 1) % m_TracePool.Count;
        return trace;
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
        float floorY = FindFloorHeightAt(stepWorldPos);
        stepWorldPos.y = floorY + 0.015f;

        Quaternion stepRotation = Quaternion.LookRotation(forwardFlat, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

        GhostTrace pooledTrace = GetNextPooledTrace();
        if (pooledTrace != null)
        {
            pooledTrace.ActivateTrace(
                stepWorldPos,
                stepRotation,
                Vector3.up,
                new Vector3(0.22f, 0.32f, 1.0f),
                traceLifetimeSeconds,
                GhostTrace.TraceVisualType.Footprint,
                footprintUVColor
            );
        }
    }

    private void SpawnEscapeOriginRing(Vector3 worldPos)
    {
        if (!leaveEscapeOriginMark || m_TracePool.Count == 0)
            return;

        float floorY = FindFloorHeightAt(worldPos);
        Vector3 ringPos = new Vector3(worldPos.x, floorY + 0.018f, worldPos.z);
        Quaternion flatRot = Quaternion.Euler(90f, 0f, 0f);

        GhostTrace trace = GetNextPooledTrace();
        if (trace != null)
        {
            trace.ActivateTrace(
                ringPos,
                flatRot,
                Vector3.up,
                new Vector3(0.55f, 0.55f, 1.0f),
                wallMarkLifetimeSeconds,
                GhostTrace.TraceVisualType.ResidualRing,
                footprintUVColor
            );
        }
    }

    private void TrySpawnWallPhaseMark(bool isEnteringWall)
    {
        if (!leaveWallPhaseUVMarks || m_TracePool.Count == 0)
            return;

        if (Time.time - m_LastWallMarkTime < 0.25f)
            return;

        Vector3 chestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        Vector3 travelDir = m_LastPathForward.sqrMagnitude > 0.001f ? m_LastPathForward.normalized : transform.forward;
        travelDir.y = 0f;
        if (travelDir.sqrMagnitude < 0.001f)
            travelDir = Vector3.forward;
        else
            travelDir.Normalize();

        // When entering a wall, cast forward from slightly behind; when exiting, cast backward toward the wall we just left!
        Vector3 rayDir = isEnteringWall ? travelDir : -travelDir;
        Vector3 rayStart = chestCenter - (rayDir * 0.85f);

        if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, 1.9f, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
            {
                // Make sure we hit a vertical wall rather than the floor
                if (Mathf.Abs(hit.normal.y) < 0.45f)
                {
                    m_LastWallMarkTime = Time.time;
                    Vector3 wallSurfacePos = hit.point + hit.normal * 0.02f;
                    Quaternion wallRot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                    Vector3 riftScale = new Vector3(wallMarkSize, wallMarkSize * 1.25f, 1.0f);

                    GhostTrace trace = GetNextPooledTrace();
                    if (trace != null)
                    {
                        trace.ActivateTrace(
                            wallSurfacePos,
                            wallRot,
                            hit.normal,
                            riftScale,
                            wallMarkLifetimeSeconds,
                            GhostTrace.TraceVisualType.WallPhaseRift,
                            wallMarkUVColor
                        );
                    }
                }
            }
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

        return m_BaseYPosition - 0.98f;
    }

    // =========================================================================
    // SUPERNATURAL WALL-PHASING DETECTION
    // =========================================================================
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
            Debug.Log($"[{gameObject.name}] Phasing through solid wall (Spectral Shift + UV Wall Rift spawned)!");
            TrySpawnWallPhaseMark(isEnteringWall: true);
        }
        else if (!isPhasingThroughWall && m_WasPhasingLastFrame)
        {
            // Also leave an exit rift mark on the far side of the wall!
            TrySpawnWallPhaseMark(isEnteringWall: false);
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

    // =========================================================================
    // AI STATE UPDATES (HIDDEN, STALKING, WATCHING, FLEEING + STRUGGLE)
    // =========================================================================
    private void UpdateHidden()
    {
        if (triggerOnFlashlightHit && m_GhostTarget != null && m_GhostTarget.IsIlluminated)
        {
            TriggerFakeEscape("Normal Flashlight beam spotted the hidden ghost!");
            return;
        }

        // Stage 1: UV Cloak Shimmer -> Spook after uvDisturbDuration if player holds UV on her
        if (m_GhostTarget != null && m_GhostTarget.IsUVIlluminated)
        {
            if (disturbWithUVBeam)
            {
                m_UVExposureTimer += Time.deltaTime;
                if (m_UVExposureTimer >= uvDisturbDuration)
                {
                    m_UVExposureTimer = 0f;
                    TriggerFakeEscape("UV Flashlight exposed the hidden ghost!");
                    return;
                }
            }
        }
        else
        {
            m_UVExposureTimer = Mathf.Max(0f, m_UVExposureTimer - Time.deltaTime * 2.0f);
        }

        if (playerHead != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerHead.position);
            if (distanceToPlayer <= triggerProximityDistance)
            {
                if (!requireLineOfSightForProximity || HasLineOfSightToPlayer())
                {
                    TriggerFakeEscape("Player approached hiding spot at close range!");
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
        Debug.Log($"[{gameObject.name}] Player hasn't found ghost for {stalkStartDelaySeconds:F0}s -> Entering STALKING Phantom state!");
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
            ApplyHoverAndStruggleOffset(m_BaseYPosition);
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
            Vector3 directionToPlayer = playerHead.position - m_CorePathPosition;
            directionToPlayer.y = 0f;

            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // Keep core spot anchored while applying hover + Stage 2 beam struggle vibration!
        Vector3 struggleJitter = CalculateBeamStruggleOffset();
        float hoverY = m_BaseYPosition + CalculateHoverAndShimmerOffset();
        transform.position = new Vector3(
            m_CorePathPosition.x + struggleJitter.x,
            hoverY + struggleJitter.y,
            m_CorePathPosition.z + struggleJitter.z
        );

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

        Vector3 targetPos = m_TargetSpot.Position;
        bool isIlluminated = m_GhostTarget != null && m_GhostTarget.IsIlluminated;
        float captureProgress = m_GhostTarget != null ? m_GhostTarget.CurrentProgressNormalized : 0f;

        // 1. Reactive Flashlight Panic + Stage 2 Beam-Lock Slowdown!
        float activeSpeed = fleeSpeed;
        float activeWeaveAmp = weaveAmplitude;

        if (enablePanicDodge && isIlluminated)
        {
            activeSpeed *= illuminatedSpeedMultiplier;
            activeWeaveAmp *= illuminatedWeaveMultiplier;

            m_TimeUnderBeamWhileFleeing += Time.deltaTime;
            if (m_TimeUnderBeamWhileFleeing >= jukeIntervalUnderBeam)
            {
                m_TimeUnderBeamWhileFleeing = 0f;
                m_JukeSign = -m_JukeSign;
            }
        }
        else
        {
            m_TimeUnderBeamWhileFleeing = Mathf.Max(0f, m_TimeUnderBeamWhileFleeing - Time.deltaTime * 2f);
        }

        // Smoothly curve dodge side changes instead of snapping left/right in 1 frame
        m_SmoothedJukeSign = Mathf.SmoothDamp(m_SmoothedJukeSign, m_JukeSign, ref m_JukeSignVelocity, 0.48f);

        // Stage 2: As capture progress rises (0% -> 100%), the beam drags down her speed ("Beam-Lock")!
        if (enableBeamStruggle && captureProgress > 0.01f)
        {
            float beamDragMultiplier = Mathf.Lerp(1.0f, 1.0f - maxBeamSlowdownFactor, captureProgress);
            activeSpeed *= beamDragMultiplier;
            activeWeaveAmp *= Mathf.Lerp(1.0f, 0.35f, captureProgress);
        }

        activeSpeed *= Mathf.Lerp(1f, wallPhaseSpeedMultiplier, m_CurrentWallPhaseBlend);

        // 2. Advance Core Path Toward Hiding Spot
        m_CorePathPosition = Vector3.MoveTowards(m_CorePathPosition, targetPos, activeSpeed * Time.deltaTime);

        Vector3 remainingToTarget = targetPos - m_CorePathPosition;
        Vector3 flatRemaining = new Vector3(remainingToTarget.x, 0f, remainingToTarget.z);
        float remainingFlatDistance = flatRemaining.magnitude;

        if (flatRemaining.sqrMagnitude > 0.0001f)
        {
            m_LastPathForward = flatRemaining / remainingFlatDistance;
        }

        // 3. Smooth Floating S-Curve Lateral Glide (Critically Damped, Zero Sharp Corners)
        float desiredLateralOffset = 0f;
        m_FleeElapsedSeconds += Time.deltaTime;
        if (enableEvasiveWeaving && remainingFlatDistance > 0.05f)
        {
            m_FleePhaseTime += Time.deltaTime * weaveFrequency * Mathf.PI * 2f;

            float sineWave = Mathf.Sin(m_FleePhaseTime);
            float perlinWave = (Mathf.PerlinNoise(Time.time * weaveFrequency * 0.65f, m_NoiseSeed) - 0.5f) * 2.0f;
            float combinedWave = Mathf.Lerp(sineWave, perlinWave, erraticNoiseBlend) * m_SmoothedJukeSign;

            float arrivalTaper = arrivalTaperDistance > 0.01f
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remainingFlatDistance / arrivalTaperDistance))
                : 1.0f;
            float startRamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m_FleeElapsedSeconds / 0.65f));
            float wallWeaveDamping = Mathf.Lerp(1.0f, 0.25f, m_CurrentWallPhaseBlend);

            desiredLateralOffset = combinedWave * activeWeaveAmp * arrivalTaper * startRamp * wallWeaveDamping;
        }

        float effectiveSmoothTime = Mathf.Max(0.10f, weaveSmoothTime);
        m_CurrentLateralOffset = Mathf.SmoothDamp(
            m_CurrentLateralOffset,
            desiredLateralOffset,
            ref m_LateralOffsetVelocity,
            effectiveSmoothTime
        );

        // 4. Final World Position (Core Path + Smooth Glide + Hover + Stage 2 Struggle Shiver)
        Vector3 pathRight = Vector3.Cross(Vector3.up, m_LastPathForward).normalized;
        float hoverY = m_CorePathPosition.y + CalculateHoverAndShimmerOffset();
        Vector3 struggleJitter = CalculateBeamStruggleOffset();

        Vector3 newVisualPos = new Vector3(
            m_CorePathPosition.x + (pathRight.x * m_CurrentLateralOffset) + struggleJitter.x,
            hoverY + struggleJitter.y,
            m_CorePathPosition.z + (pathRight.z * m_CurrentLateralOffset) + struggleJitter.z
        );
        transform.position = newVisualPos;

        // 5. Smooth Forward-Biased Heading (Prevents whipping 70 degrees left/right) & UV Trail
        Vector3 gentleGlideDir = m_LastPathForward + (pathRight * Mathf.Clamp(m_LateralOffsetVelocity * 0.14f, -0.32f, 0.32f));
        if (gentleGlideDir.sqrMagnitude > 0.0001f)
        {
            Vector3 normalizedDir = gentleGlideDir.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(normalizedDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * 0.75f * Time.deltaTime);
            UpdateTraceTrail(normalizedDir);
        }
        else
        {
            UpdateTraceTrail(m_LastPathForward);
        }

        // 6. Check Arrival at Hiding Spot (+ Stage 1 Anti-Frustration Beam Burn-Lock!)
        if (Vector3.Distance(m_CorePathPosition, targetPos) <= 0.1f)
        {
            bool heldInFlashlightBeam = preventCloakWhileIlluminated
                && m_GhostTarget != null
                && m_GhostTarget.IsRecentlyIlluminated;

            if (heldInFlashlightBeam && hidingSpots.Count > 1)
            {
                m_PreviousSpot = m_CurrentSpot;
                m_CurrentSpot = m_TargetSpot;
                Debug.Log($"[{gameObject.name}] Reached '{m_CurrentSpot.SpotName}' while illuminated! Beam Burn-Lock prevents cloaking — bolting to a new spot!");
                StartFleeing();
                return;
            }

            m_PreviousSpot = m_CurrentSpot;
            m_CurrentSpot = m_TargetSpot;
            m_CorePathPosition = m_CurrentSpot.Position;
            transform.position = m_CurrentSpot.Position;
            m_BaseYPosition = m_CurrentSpot.Position.y;
            m_CurrentLateralOffset = 0f;
            m_LateralOffsetVelocity = 0f;
            m_HiddenTimer = 0f;
            m_UVExposureTimer = 0f;
            currentState = GhostState.Hidden;
            Debug.Log($"[{gameObject.name}] Ghost safely reached hiding spot '{m_CurrentSpot.SpotName}' — dematerializing into cloak.");
        }
    }

    // =========================================================================
    // STAGE 2: BEAM STRUGGLE VIBRATION & HOVER HELPERS
    // =========================================================================
    private Vector3 CalculateBeamStruggleOffset()
    {
        if (!enableBeamStruggle || m_GhostTarget == null || !m_GhostTarget.IsIlluminated)
            return Vector3.zero;

        float progress = m_GhostTarget.CurrentProgressNormalized;
        if (progress <= 0.15f)
            return Vector3.zero;

        // Quadratic ramp so she glides cleanly at first and shivers intensely right before the 3s capture!
        float ramp = (progress - 0.15f) / 0.85f;
        float strength = struggleTrembleAmplitude * (ramp * ramp);
        float t = Time.time * struggleTrembleFrequency;
        return new Vector3(
            Mathf.Sin(t * 1.13f) * strength,
            Mathf.Cos(t * 1.71f) * (strength * 0.45f),
            Mathf.Sin(t * 0.89f + 1.7f) * strength
        );
    }

    private float CalculateHoverAndShimmerOffset()
    {
        float normalHover = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        if (m_CurrentWallPhaseBlend <= 0.001f)
            return normalHover;

        float phaseShimmer = Mathf.Sin(Time.time * phaseShimmerFrequency) * (hoverAmplitude * 0.45f) * m_CurrentWallPhaseBlend;
        return normalHover + phaseShimmer;
    }

    private void ApplyHoverAndStruggleOffset(float baseY)
    {
        Vector3 jitter = CalculateBeamStruggleOffset();
        float hoverY = baseY + CalculateHoverAndShimmerOffset();
        transform.position = new Vector3(transform.position.x + jitter.x, hoverY + jitter.y, transform.position.z + jitter.z);
    }

    // =========================================================================
    // STAGE 5: 60-SECOND SEARCH HINT EMISSION
    // =========================================================================
    /// <summary>
    /// Called by RoundManager when the player hasn't spotted the ghost for 60 seconds.
    /// Pulses a brief spectral silhouette shimmer and drops UV breadcrumb rings leading toward her!
    /// </summary>
    [ContextMenu("Emit Search Hint Now")]
    public void EmitSearchHint()
    {
        if (currentState == GhostState.Captured)
            return;

        m_HintShimmerTimer = hintFlickerDuration;

        if (m_TracePool.Count > 0 && hintBreadcrumbCount > 0)
        {
            Vector3 ghostPos = transform.position;
            Vector3 playerPos = playerHead != null ? playerHead.position : (ghostPos + Vector3.forward * 4f);
            Vector3 toGhostFlat = new Vector3(ghostPos.x - playerPos.x, 0f, ghostPos.z - playerPos.z);
            float totalDist = toGhostFlat.magnitude;
            Vector3 dirToGhost = totalDist > 0.1f ? (toGhostFlat / totalDist) : Vector3.forward;

            // Place a breadcrumb trail starting ~3.5m away from the ghost leading right to her hiding spot!
            float trailLength = Mathf.Min(3.8f, Mathf.Max(1.2f, totalDist * 0.65f));
            for (int i = 0; i < hintBreadcrumbCount; i++)
            {
                float frac = hintBreadcrumbCount > 1 ? (float)i / (hintBreadcrumbCount - 1) : 1.0f;
                float backOffset = Mathf.Lerp(trailLength, 0.25f, frac);
                Vector3 crumbPos = ghostPos - (dirToGhost * backOffset);
                crumbPos.y = FindFloorHeightAt(crumbPos) + 0.018f;

                GhostTrace trace = GetNextPooledTrace();
                if (trace != null)
                {
                    trace.ActivateTrace(
                        crumbPos,
                        Quaternion.Euler(90f, 0f, 0f),
                        Vector3.up,
                        new Vector3(0.45f, 0.45f, 1.0f),
                        wallMarkLifetimeSeconds,
                        GhostTrace.TraceVisualType.ResidualRing,
                        wallMarkUVColor
                    );
                }
            }
        }

        Debug.Log($"[{gameObject.name}] *** SEARCH HINT EMITTED! *** Brief spectral shimmer + {hintBreadcrumbCount} UV breadcrumb rings spawned toward '{(m_CurrentSpot != null ? m_CurrentSpot.SpotName : "Ghost")}'.");
    }

    // =========================================================================
    // FAKE ESCAPE, ROUND RESET & TACTICAL HIDING SPOT SCORING
    // =========================================================================
    [ContextMenu("Trigger Fake Escape")]
    public void TriggerFakeEscape(string reason = "Triggered")
    {
        if (currentState != GhostState.Hidden && currentState != GhostState.Stalking)
            return;

        m_HiddenTimer = 0f;
        m_StalkTimer = 0f;
        m_UVExposureTimer = 0f;
        m_HintShimmerTimer = 0f;
        m_BaseYPosition = transform.position.y;
        m_CorePathPosition = transform.position;

        bool chooseWatch = Random.value <= watchPlayerChance;

        if (chooseWatch)
        {
            currentState = GhostState.WatchingPlayer;
            m_WatchTimer = watchPlayerDuration;
            m_WasIlluminatedDuringStare = false;
            m_StareBeamLostGraceTimer = stareBeamLossGraceSeconds;
            Debug.Log($"[{gameObject.name}] {reason} -> Materializing into Fake Escape [Behaviour A: Staring for {watchPlayerDuration:F1}s]");
        }
        else
        {
            Debug.Log($"[{gameObject.name}] {reason} -> Materializing into Fake Escape [Behaviour B: Immediate Escape!]");
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
        m_UVExposureTimer = 0f;
        m_HintShimmerTimer = 0f;
        m_CurrentLateralOffset = 0f;
        m_LateralOffsetVelocity = 0f;
        m_FleeElapsedSeconds = 0f;
        m_CurrentWallPhaseBlend = 0f;
        m_WasPhasingLastFrame = false;
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
        currentSpectralVisibility = cloakWhileHidden ? 0.0f : 1.0f;

        if (m_GhostTarget != null)
        {
            m_GhostTarget.ResetTarget();
            m_GhostTarget.SetSpectralState(currentSpectralVisibility, false, s_PhantomRimColor, 0f);
        }
        else
        {
            gameObject.SetActive(true);
        }

        Debug.Log($"[{gameObject.name}] Respawned (Cloaked={cloakWhileHidden}) at '{(m_CurrentSpot != null ? m_CurrentSpot.SpotName : "Start")}' | Speed: {fleeSpeed:F2} m/s | Weave: {weaveAmplitude:F2}m @ {weaveFrequency:F1}Hz | Stare: {watchPlayerDuration:F1}s");
    }

    private void StartFleeing()
    {
        Vector3 escapeOrigin = transform.position;
        m_TargetSpot = GetSmartNextHidingSpot();
        m_CorePathPosition = escapeOrigin;
        m_LastTraceDropPosition = escapeOrigin;
        m_CurrentLateralOffset = 0f;
        m_LateralOffsetVelocity = 0f;
        m_FleePhaseTime = 0f;
        m_FleeElapsedSeconds = 0f;
        m_TimeUnderBeamWhileFleeing = 0f;
        m_JukeSign = Random.value > 0.5f ? 1f : -1f;
        m_SmoothedJukeSign = m_JukeSign;
        m_JukeSignVelocity = 0f;
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

        // Stage 3: Leave a pulsing UV Residual Ring at the hiding spot she bolted from!
        SpawnEscapeOriginRing(escapeOrigin);
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
        Debug.Log($"[{gameObject.name}] Ghost captured! AI stopped while Capture Vanish plays.");
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
