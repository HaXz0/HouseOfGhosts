using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-20)]
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

    public enum WallPhaseTierOverride
    {
        UseCurrentRound = 0,
        ForceEarlySplash = 1,
        ForceLateOutlineAndSpeed = 2
    }

    [Header("Current State (Read Only)")]
    [SerializeField] private GhostState currentState = GhostState.Hidden;
    [SerializeField] private int activeRoundNumber = 1;
    [SerializeField] private bool isPhasingThroughWall = false;
    [SerializeField] private bool isCloakedWhileFleeing = false;
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

    [Tooltip("Ghostly UV silhouette visibility (0.48 = 48%) when you shine the UV flashlight onto a cloaked ghost.")]
    [Range(0.15f, 0.85f)]
    [SerializeField] private float uvCloakShimmerVisibility = 0.48f;

    [Tooltip("PURPLE rim/aura color when the UV beam reveals a cloaked/invisible ghost.")]
    [SerializeField] private Color uvShimmerRimColor = new Color(0.58f, 0.22f, 1.0f, 1.0f);

    [Tooltip("Spectral BLUE aura color when the ghost is exposed and visible to the naked eye (without UV).")]
    [SerializeField] private Color exposedVisibleBlueColor = new Color(0.16f, 0.72f, 1.0f, 1.0f);

    [Tooltip("If true, holding the UV beam on a Hidden ghost for uvDisturbDuration spooks her into Fake Escape.")]
    [SerializeField] private bool disturbWithUVBeam = true;

    [Tooltip("Seconds of continuous UV beam exposure before a Hidden ghost bolts (gives you time to see her purple UV aura & switch to Normal light!).")]
    [SerializeField] private float uvDisturbDuration = 2.2f;

    [Header("Stage 1B: Invisible Flee (Stealth Escape)")]
    [Tooltip("If true, when detected and running away, the ghost has a chance (cloakedFleeChance) to cloak into invisibility!")]
    [SerializeField] private bool enableCloakedFlee = true;

    [Tooltip("Chance (0.15 = 15%) for the ghost to turn invisible when running away! Drag to 1.0 (100%) to test every escape.")]
    [Range(0f, 1f)]
    [SerializeField] private float cloakedFleeChance = 0.15f;

    [Tooltip("Distance in meters (default 1.5m): Normal Flashlight does NOTHING to an invisible fleeing ghost until you get within this distance AND aim the flashlight at her!")]
    [SerializeField] private float cloakedFleeRevealDistance = 1.5f;

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

    [Tooltip("BLUE UV trace color used for ALL traces (Footprints, Escape Ring, Wall Splash, and Rounds 4-6 Ghost Outline) dropped while the ghost is VISIBLE!")]
    [SerializeField] private Color visibleStateTraceUVColor = new Color(0.16f, 0.72f, 1.0f, 1.0f);

    [Tooltip("PURPLE UV trace color used for ALL traces (Footprints, Escape Ring, Wall Splash, Rounds 4-6 Ghost Outline, and Search Hints) dropped while the ghost is INVISIBLE!")]
    [SerializeField] private Color invisibleStateTraceUVColor = new Color(0.62f, 0.22f, 1.0f, 1.0f);

    [Tooltip("If true, passing through a solid wall leaves glowing UV Splash (Early) or Ghost Outline (Late) marks on BOTH sides of the wall!")]
    [SerializeField] private bool leaveWallPhaseUVMarks = true;

    [Tooltip("Seconds that UV Wall Marks (Splash / Ghost Outline) stay discoverable.")]
    [SerializeField] private float wallMarkLifetimeSeconds = 32.0f;

    [Tooltip("If true, bolting from a hiding spot leaves a pulsing UV Residual Ring at the spot she escaped from.")]
    [SerializeField] private bool leaveEscapeOriginMark = true;

    [Tooltip("Number of reusable UV trace objects in the pool (zero runtime garbage collection).")]
    [SerializeField] private int tracePoolSize = 32;

    [Tooltip("Material using HouseOfGhosts/UVGhostTrace shader for UV traces.")]
    [SerializeField] private Material uvTraceMaterial;

    [Header("Stage 3B: Round-Based Wall-Phase Tiers (Splash vs. Outline + Speed)")]
    [Tooltip("Round where Wall-Phasing upgrades from Splash (Scenario 1) to Super-Fast Surge + Ghost Outline (Scenario 2). Default 4 = Rounds 1-3 Splash, Rounds 4-6 Outline + Speed!")]
    [Range(1, 6)]
    [SerializeField] private int lateWallPhaseStartRound = 4;

    [Tooltip("Testing shortcut! Leave on UseCurrentRound for normal gameplay, or force Early Splash / Late Outline & Speed in Round 1.")]
    [SerializeField] private WallPhaseTierOverride wallPhaseTestOverride = WallPhaseTierOverride.UseCurrentRound;

    [Tooltip("Diameter (meters) of the organic UV Ectoplasm Splash left on walls in Early Rounds (1-3).")]
    [SerializeField] private float earlyWallSplashSize = 0.95f;

    [Tooltip("Super-fast forward speed multiplier WHILE passing through a solid wall in Late Rounds (4-6)!")]
    [SerializeField] private float lateWallPhaseSpeedMultiplier = 2.35f;

    [Tooltip("Extra post-wall speed boost multiplier right after bursting through a wall in Late Rounds (4-6)!")]
    [SerializeField] private float lateWallSpeedBoostMultiplier = 1.45f;

    [Tooltip("Seconds that the post-wall speed surge lasts before smoothly settling back to normal round speed.")]
    [SerializeField] private float lateWallSpeedBoostDuration = 1.6f;

    [Tooltip("Padding around the ghost's body dimensions so the glowing UV outline aura never clips at the quad border.")]
    [Range(1.02f, 1.35f)]
    [SerializeField] private float ghostOutlinePadding = 1.14f;

    [Tooltip("Leave empty right now to render a crisp Capsule Outline! When the 3D Ghost Girl model is finished, drag her 2D silhouette texture (alpha PNG) here to upgrade the wall outline automatically.")]
    [SerializeField] private Texture2D customGhostSilhouetteTexture;

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

    // Wall-phasing & Stage 3B round-based wall-mark state
    private Vector3 m_InitialLocalScale = Vector3.one;
    private float m_RestingBodyWidth = 1.0f;
    private float m_RestingBodyHeight = 2.0f;
    private float m_CurrentWallPhaseBlend = 0f;
    private float m_LateWallSurgeTimer = 0f;
    private bool m_WasPhasingLastFrame = false;
    private float m_LastWallEntryMarkTime = -10f;
    private float m_LastWallExitMarkTime = -10f;
    private Vector3 m_PreviousChestCenter;
    private Collider m_LastOverlappedWallCollider;
    private readonly Collider[] m_WallOverlapBuffer = new Collider[12];
    private readonly RaycastHit[] m_WallRayHits = new RaycastHit[16];

    // UV Trace Pool state
    private readonly List<GhostTrace> m_TracePool = new List<GhostTrace>();
    private int m_NextTracePoolIndex = 0;
    private Vector3 m_LastTraceDropPosition;
    private bool m_NextFootIsRight = true;
    private Transform m_TracePoolContainer;

    private bool m_ForceNextEscapeCloaked = false;

    public GhostState CurrentState => currentState;
    public bool IsPhasingThroughWall => isPhasingThroughWall;
    public bool IsCloakedWhileFleeing => isCloakedWhileFleeing;
    public float CurrentSpectralVisibility => currentSpectralVisibility;

    /// <summary>
    /// True whenever the ghost is operating invisibly (Hidden or Invisible Flee) -> Purple UV aura & Purple UV traces!
    /// False whenever the ghost is exposed/visible (Stalking, WatchingPlayer, or Visible Flee) -> Blue body & Blue UV traces!
    /// </summary>
    public bool IsGhostCurrentlyInvisible =>
        cloakWhileHidden && (currentState == GhostState.Hidden || (currentState == GhostState.Fleeing && isCloakedWhileFleeing));

    private Color GetActiveTraceUVColor() =>
        IsGhostCurrentlyInvisible ? invisibleStateTraceUVColor : visibleStateTraceUVColor;

    /// <summary>
    /// True in Late Rounds (default Rounds 4-6) or when ForceLateOutlineAndSpeed is selected in the Inspector.
    /// </summary>
    public bool IsLateWallPhaseTier
    {
        get
        {
            if (wallPhaseTestOverride == WallPhaseTierOverride.ForceEarlySplash)
                return false;
            if (wallPhaseTestOverride == WallPhaseTierOverride.ForceLateOutlineAndSpeed)
                return true;

            if (RoundManager.Instance != null)
            {
                activeRoundNumber = RoundManager.Instance.CurrentRound;
            }
            return activeRoundNumber >= lateWallPhaseStartRound;
        }
    }

    private void Awake()
    {
        m_GhostTarget = GetComponent<GhostTarget>();
        m_BaseYPosition = transform.position.y;
        m_CorePathPosition = transform.position;
        m_InitialLocalScale = transform.localScale;
        CacheRestingBodyDimensions();
        m_PreviousChestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
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
        m_PreviousChestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;

        if (m_GhostTarget == null || !m_GhostTarget.IsTestDummy)
        {
            InitializeTracePool();
        }

        // Start cloaked if starting in Hidden state
        bool startsCloaked = cloakWhileHidden && currentState == GhostState.Hidden;
        currentSpectralVisibility = startsCloaked ? 0f : 1f;
        PushSpectralStateToTarget(
            currentSpectralVisibility,
            false,
            startsCloaked ? uvShimmerRimColor : exposedVisibleBlueColor,
            startsCloaked ? 0f : 1.45f
        );
    }

    private void CacheRestingBodyDimensions()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            m_RestingBodyWidth = Mathf.Max(0.4f, cap.radius * 2.0f * Mathf.Abs(m_InitialLocalScale.x));
            m_RestingBodyHeight = Mathf.Max(0.9f, cap.height * Mathf.Abs(m_InitialLocalScale.y));
            return;
        }

        Renderer rend = GetComponentInChildren<Renderer>(true);
        if (rend != null)
        {
            Vector3 localSize = rend.localBounds.size;
            m_RestingBodyWidth = Mathf.Max(0.4f, localSize.x * Mathf.Abs(m_InitialLocalScale.x));
            m_RestingBodyHeight = Mathf.Max(0.9f, localSize.y * Mathf.Abs(m_InitialLocalScale.y));
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

        UpdateCloakAndSpectralPresence();
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

    // =========================================================================
    // STAGE 1 & 1B: CLOAK, INVISIBLE FLEE, BLUE (VISIBLE) VS PURPLE (INVISIBLE)
    // =========================================================================
    private void UpdateCloakAndSpectralPresence()
    {
        if (!cloakWhileHidden)
        {
            isCloakedWhileFleeing = false;
            if (m_GhostTarget != null)
                m_GhostTarget.SetCloakCaptureImmunity(false);

            currentSpectralVisibility = 1.0f;
            PushSpectralStateToTarget(1.0f, false, exposedVisibleBlueColor, 1.45f);
            return;
        }

        if (m_HintShimmerTimer > 0f)
        {
            m_HintShimmerTimer -= Time.deltaTime;
        }

        bool hitByNormalBeam = m_GhostTarget != null && m_GhostTarget.IsIlluminated;
        bool hitByUVBeam = m_GhostTarget != null && m_GhostTarget.IsUVIlluminated;

        // Stage 1B: Break Invisible Flee cloak ONLY when player is within cloakedFleeRevealDistance (1.5m) AND aiming the Normal Flashlight at her!
        if (currentState == GhostState.Fleeing && isCloakedWhileFleeing)
        {
            if (m_FleeElapsedSeconds >= 0.35f && hitByNormalBeam && GetPlayerDistanceToGhostBody() <= cloakedFleeRevealDistance)
            {
                isCloakedWhileFleeing = false;
                if (m_GhostTarget != null)
                {
                    m_GhostTarget.SetCloakCaptureImmunity(false);
                }
                Debug.Log($"[{gameObject.name}] *** STEALTH CLOAK SHATTERED! *** Player aimed flashlight within {cloakedFleeRevealDistance:F1}m — Ghost exposed (BLUE) & vulnerable!");
            }
        }
        else if (isCloakedWhileFleeing)
        {
            isCloakedWhileFleeing = false;
        }

        if (m_GhostTarget != null)
        {
            m_GhostTarget.SetCloakCaptureImmunity(currentState == GhostState.Fleeing && isCloakedWhileFleeing);
        }

        float targetVisibility = 1.0f;
        float transitionSpeed = 1.0f / Mathf.Max(0.08f, materializeDuration);
        Color rimColor = exposedVisibleBlueColor;
        float rimIntensity = 1.45f;

        if (currentState == GhostState.Hidden || (currentState == GhostState.Fleeing && isCloakedWhileFleeing))
        {
            // INVISIBLE / CLOAKED MODE: 0% to naked eye, PURPLE aura only under UV!
            rimColor = uvShimmerRimColor;

            if (hitByUVBeam)
            {
                float uvPulse = 0.88f + 0.12f * Mathf.Sin(Time.time * 9.0f);
                targetVisibility = uvCloakShimmerVisibility * uvPulse;
                m_IsUsingHeightWipe = false;
                transitionSpeed = 4.5f;
                rimIntensity = 1.85f;
            }
            else if (currentState == GhostState.Hidden && m_HintShimmerTimer > 0f)
            {
                // Stage 5: Brief 60s Search Hint spectral apparition pulse
                float hintWave = Mathf.Sin((1.0f - (m_HintShimmerTimer / Mathf.Max(0.1f, hintFlickerDuration))) * Mathf.PI);
                targetVisibility = 0.48f * Mathf.Clamp01(hintWave * 1.3f);
                m_IsUsingHeightWipe = false;
                transitionSpeed = 3.5f;
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
                    rimIntensity = 1.65f;
                }
                else
                {
                    rimIntensity = 1.25f;
                }
            }
        }
        else if (currentState == GhostState.Stalking)
        {
            // VISIBLE STALKING: Semi-ethereal BLUE phantom form (visible without UV)
            float eerieFlicker = (Mathf.PerlinNoise(Time.time * 3.8f, m_NoiseSeed) - 0.5f) * 0.14f;
            targetVisibility = Mathf.Clamp01(stalkPhantomVisibility + eerieFlicker);
            m_IsUsingHeightWipe = false;
            transitionSpeed = 2.2f;
            rimColor = exposedVisibleBlueColor;
            rimIntensity = 1.25f;
        }
        else
        {
            // EXPOSED WATCHING OR VISIBLE FLEEING: Full 100% Materialization in Spectral BLUE (without UV)!
            targetVisibility = 1.0f;
            transitionSpeed = 1.0f / Mathf.Max(0.08f, materializeDuration);
            rimColor = exposedVisibleBlueColor;

            if (currentSpectralVisibility < 0.98f)
            {
                m_IsUsingHeightWipe = true;
                rimIntensity = Mathf.Lerp(1.85f, 1.45f, currentSpectralVisibility);
            }
            else
            {
                m_IsUsingHeightWipe = false;
                rimIntensity = 1.45f;
            }
        }

        // Anti-Frustration Beam Burn-Lock: Normal beam burns away cloak ONLY when not stealth-immune!
        if (hitByNormalBeam && currentState != GhostState.Hidden && !isCloakedWhileFleeing)
        {
            targetVisibility = 1.0f;
        }

        currentSpectralVisibility = Mathf.MoveTowards(currentSpectralVisibility, targetVisibility, transitionSpeed * Time.deltaTime);
        PushSpectralStateToTarget(currentSpectralVisibility, m_IsUsingHeightWipe, rimColor, rimIntensity);
    }

    private float GetPlayerDistanceToGhostBody()
    {
        if (playerHead == null)
            return float.MaxValue;

        Vector3 center = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        float halfHeight = m_RestingBodyHeight * 0.5f;
        float clampedY = Mathf.Clamp(playerHead.position.y, center.y - halfHeight, center.y + halfHeight);
        Vector3 closestTorsoPoint = new Vector3(center.x, clampedY, center.z);
        return Vector3.Distance(playerHead.position, closestTorsoPoint);
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

    private GhostTrace GetNextPooledTrace(bool isHighPriorityWallMark = false)
    {
        int count = m_TracePool.Count;
        if (count == 0)
            return null;

        // 1. Prefer an inactive pooled trace first
        for (int i = 0; i < count; i++)
        {
            int idx = (m_NextTracePoolIndex + i) % count;
            GhostTrace candidate = m_TracePool[idx];
            if (candidate != null && !candidate.gameObject.activeSelf)
            {
                m_NextTracePoolIndex = (idx + 1) % count;
                return candidate;
            }
        }

        // 2. If all traces are active and we are spawning a Footprint/Ring, protect active Wall Splashes & Ghost Outlines!
        if (!isHighPriorityWallMark)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = (m_NextTracePoolIndex + i) % count;
                GhostTrace candidate = m_TracePool[idx];
                if (candidate != null && !candidate.IsWallMark)
                {
                    m_NextTracePoolIndex = (idx + 1) % count;
                    return candidate;
                }
            }
        }

        GhostTrace fallback = m_TracePool[m_NextTracePoolIndex];
        m_NextTracePoolIndex = (m_NextTracePoolIndex + 1) % count;
        return fallback;
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
                GetActiveTraceUVColor()
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
                GetActiveTraceUVColor()
            );
        }
    }

    private void TrySpawnWallPhaseMark(bool isEnteringWall)
    {
        if (!leaveWallPhaseUVMarks || m_TracePool.Count == 0)
            return;

        // Independent entry & exit cooldowns so fast Late-Round wall passes (~0.10s) always stamp BOTH sides of a wall!
        float lastTime = isEnteringWall ? m_LastWallEntryMarkTime : m_LastWallExitMarkTime;
        if (Time.time - lastTime < 0.15f)
            return;

        Vector3 chestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        Vector3 travelDir = m_LastPathForward.sqrMagnitude > 0.001f ? m_LastPathForward.normalized : transform.forward;
        travelDir.y = 0f;
        if (travelDir.sqrMagnitude < 0.001f)
            travelDir = Vector3.forward;
        else
            travelDir.Normalize();

        // When entering a wall, cast forward from behind the ghost; when exiting, cast backward from ahead of the ghost!
        Vector3 rayDir = isEnteringWall ? travelDir : -travelDir;
        Vector3 rayStart = chestCenter - (rayDir * 1.45f);

        if (TryFindWallSurfaceHit(rayStart, rayDir, 3.2f, out RaycastHit hit) ||
            TryFallbackOverlappedWallHit(rayStart, chestCenter.y, out hit))
        {
            if (isEnteringWall)
                m_LastWallEntryMarkTime = Time.time;
            else
                m_LastWallExitMarkTime = Time.time;

            SpawnWallMarkAtHit(hit, chestCenter.y, isEnteringWall);
        }
    }

    private bool TryFindWallSurfaceHit(Vector3 rayStart, Vector3 rayDir, float maxDistance, out RaycastHit bestHit)
    {
        bestHit = default;
        int hitCount = Physics.RaycastNonAlloc(rayStart, rayDir, m_WallRayHits, maxDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
        if (hitCount == 0)
            return false;

        float closestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = m_WallRayHits[i];
            Collider col = h.collider;
            if (col == null)
                continue;

            // Skip Ghost's own collider, Player rig colliders, and GhostDummy colliders!
            if (col.transform.IsChildOf(transform))
                continue;

            if (playerHead != null && col.transform.IsChildOf(playerHead.root))
                continue;

            if (col.GetComponentInParent<GhostTarget>() != null)
                continue;

            // Must be a vertical wall surface rather than floor/ceiling
            if (Mathf.Abs(h.normal.y) >= 0.45f)
                continue;

            if (h.distance < closestDist)
            {
                closestDist = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }

    private bool TryFallbackOverlappedWallHit(Vector3 sideOrigin, float ghostCenterY, out RaycastHit wallHit)
    {
        wallHit = default;
        if (m_LastOverlappedWallCollider == null)
            return false;

        Bounds wb = m_LastOverlappedWallCollider.bounds;
        Vector3 targetPoint = wb.center;
        targetPoint.y = Mathf.Clamp(ghostCenterY, wb.min.y + 0.15f, wb.max.y - 0.15f);

        Vector3 toWall = targetPoint - sideOrigin;
        float dist = toWall.magnitude;
        if (dist < 0.02f)
            return false;

        Ray ray = new Ray(sideOrigin, toWall / dist);
        if (m_LastOverlappedWallCollider.Raycast(ray, out RaycastHit hit, dist + 1.5f))
        {
            if (Mathf.Abs(hit.normal.y) < 0.45f)
            {
                wallHit = hit;
                return true;
            }
        }

        return false;
    }

    private void SpawnWallMarkAtHit(RaycastHit hit, float ghostCenterY, bool isEnteringWall)
    {
        Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
        if (wallNormal.sqrMagnitude < 0.001f)
            wallNormal = hit.normal.normalized;
        else
            wallNormal.Normalize();

        bool useLateOutline = IsLateWallPhaseTier;
        GhostTrace trace = GetNextPooledTrace(isHighPriorityWallMark: true);
        if (trace == null)
            return;

        Color activeUVColor = GetActiveTraceUVColor();
        string colorModeLabel = IsGhostCurrentlyInvisible ? "PURPLE (Invisible)" : "BLUE (Visible)";
        string sideLabel = isEnteringWall ? "Entry" : "Exit";

        if (!useLateOutline)
        {
            // Scenario 1 (Rounds 1-3): Organic UV Ectoplasm Wall Splash with unique random roll & droplet seed
            Vector3 splashPos = hit.point + wallNormal * 0.022f;
            Quaternion baseWallRot = Quaternion.LookRotation(-wallNormal, Vector3.up);
            Quaternion randomSplashRot = Quaternion.AngleAxis(Random.Range(0f, 360f), wallNormal) * baseWallRot;
            Vector3 splashScale = new Vector3(earlyWallSplashSize, earlyWallSplashSize, 1.0f);
            float splashSeed = Random.Range(0.1f, 99.9f);

            trace.ActivateTrace(
                splashPos,
                randomSplashRot,
                wallNormal,
                splashScale,
                wallMarkLifetimeSeconds,
                GhostTrace.TraceVisualType.WallSplash,
                activeUVColor,
                splashSeed,
                null,
                hit.collider
            );

            Debug.Log($"[{gameObject.name}] Spawned UV WallSplash [{colorModeLabel}, {sideLabel}] on '{hit.collider.name}' at {splashPos}");
        }
        else
        {
            // Scenario 2 (Rounds 4-6): Upright Life-Size Hollow Ghost Silhouette Outline (Blue when visible, Purple when invisible!)
            float floorY = FindFloorHeightAt(hit.point + wallNormal * 0.18f);
            float minCenterY = floorY + (m_RestingBodyHeight * 0.5f) + 0.02f;
            float outlineCenterY = Mathf.Max(minCenterY, ghostCenterY);

            Vector3 outlinePos = new Vector3(hit.point.x, outlineCenterY, hit.point.z) + wallNormal * 0.024f;
            Quaternion uprightWallRot = Quaternion.LookRotation(-wallNormal, Vector3.up);
            Vector3 outlineScale = new Vector3(
                m_RestingBodyWidth * ghostOutlinePadding,
                m_RestingBodyHeight * ghostOutlinePadding,
                1.0f
            );

            trace.ActivateTrace(
                outlinePos,
                uprightWallRot,
                wallNormal,
                outlineScale,
                wallMarkLifetimeSeconds,
                GhostTrace.TraceVisualType.GhostOutline,
                activeUVColor,
                0f,
                customGhostSilhouetteTexture,
                hit.collider
            );

            Debug.Log($"[{gameObject.name}] Spawned UV GhostOutline [{colorModeLabel}, {sideLabel}] on '{hit.collider.name}' at {outlinePos}");
        }
    }

    private float FindFloorHeightAt(Vector3 worldPos)
    {
        Vector3 rayStart = new Vector3(worldPos.x, transform.position.y + 0.8f, worldPos.z);
        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, m_WallRayHits, 6.0f, obstacleLayers, QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        float bestFloorY = m_BaseYPosition - 0.98f;
        bool foundFloor = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = m_WallRayHits[i];
            Collider col = h.collider;
            if (col == null || col.transform.IsChildOf(transform))
                continue;

            if (playerHead != null && col.transform.IsChildOf(playerHead.root))
                continue;

            if (col.GetComponentInParent<GhostTarget>() != null)
                continue;

            if (h.normal.y < 0.65f)
                continue;

            if (h.distance < closestDist)
            {
                closestDist = h.distance;
                bestFloorY = h.point.y;
                foundFloor = true;
            }
        }

        return foundFloor ? bestFloorY : (m_BaseYPosition - 0.98f);
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
            m_LateWallSurgeTimer = 0f;
            transform.localScale = m_InitialLocalScale;
            if (m_GhostTarget != null)
                m_GhostTarget.SetWallPhaseBlend(0f);
            return;
        }

        Vector3 currentChestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        bool isActivelyMoving = (currentState == GhostState.Fleeing || currentState == GhostState.Stalking);
        bool isLateTier = IsLateWallPhaseTier;

        isPhasingThroughWall = DetectWallOverlap(isActivelyMoving);

        if (isPhasingThroughWall && !m_WasPhasingLastFrame)
        {
            string tierDesc = isLateTier
                ? $"LATE TIER (Round {activeRoundNumber}) -> Super-Fast Wall Surge + UV Ghost Outline!"
                : $"EARLY TIER (Round {activeRoundNumber}) -> Normal Glide + UV Wall Splash!";
            Debug.Log($"[{gameObject.name}] Phasing through solid wall [{tierDesc}]");
            TrySpawnWallPhaseMark(isEnteringWall: true);
        }
        else if (!isPhasingThroughWall && m_WasPhasingLastFrame)
        {
            // Leave exit Splash or Ghost Outline on the far side of the wall!
            TrySpawnWallPhaseMark(isEnteringWall: false);
        }
        else if (!isPhasingThroughWall && !m_WasPhasingLastFrame && isActivelyMoving)
        {
            // Safety fallback: if a huge frame spike at high speed stepped completely across a thin wall in 1 frame
            Vector3 stepDelta = currentChestCenter - m_PreviousChestCenter;
            if (stepDelta.sqrMagnitude > 0.04f &&
                Physics.Linecast(m_PreviousChestCenter, currentChestCenter, out RaycastHit tunnelHit, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (tunnelHit.collider != null &&
                    !tunnelHit.collider.transform.IsChildOf(transform) &&
                    Mathf.Abs(tunnelHit.normal.y) < 0.45f)
                {
                    if (isLateTier)
                        m_LateWallSurgeTimer = lateWallSpeedBoostDuration;

                    TrySpawnWallPhaseMark(isEnteringWall: true);
                    TrySpawnWallPhaseMark(isEnteringWall: false);
                }
            }
        }

        m_WasPhasingLastFrame = isPhasingThroughWall;
        m_PreviousChestCenter = currentChestCenter;

        // Maintain / decay Late-Round post-wall speed surge timer
        if (isLateTier && isPhasingThroughWall)
        {
            m_LateWallSurgeTimer = lateWallSpeedBoostDuration;
        }
        else if (m_LateWallSurgeTimer > 0f)
        {
            m_LateWallSurgeTimer = Mathf.Max(0f, m_LateWallSurgeTimer - Time.deltaTime);
        }

        float targetBlend = isPhasingThroughWall ? 1.0f : 0.0f;
        float effectivePhaseRamp = isLateTier ? Mathf.Max(phaseTransitionSpeed * 2.5f, 15.0f) : phaseTransitionSpeed;
        m_CurrentWallPhaseBlend = Mathf.MoveTowards(m_CurrentWallPhaseBlend, targetBlend, effectivePhaseRamp * Time.deltaTime);

        if (m_GhostTarget != null)
        {
            // Suppress visible cyan wall-phase body glow while cloaked so walls never betray her invisibility (or overwrite Purple UV shimmer)
            m_GhostTarget.SetWallPhaseBlend(IsGhostCurrentlyInvisible ? 0f : m_CurrentWallPhaseBlend);
        }

        transform.localScale = new Vector3(
            m_InitialLocalScale.x * Mathf.Lerp(1f, phaseScaleMultiplier.x, m_CurrentWallPhaseBlend),
            m_InitialLocalScale.y * Mathf.Lerp(1f, phaseScaleMultiplier.y, m_CurrentWallPhaseBlend),
            m_InitialLocalScale.z * Mathf.Lerp(1f, phaseScaleMultiplier.z, m_CurrentWallPhaseBlend)
        );
    }

    private float GetActiveWallSpeedMultiplier()
    {
        if (!IsLateWallPhaseTier)
        {
            return Mathf.Lerp(1.0f, wallPhaseSpeedMultiplier, m_CurrentWallPhaseBlend);
        }

        float inWallMultiplier = Mathf.Lerp(1.0f, lateWallPhaseSpeedMultiplier, m_CurrentWallPhaseBlend);
        float surgeProgress = lateWallSpeedBoostDuration > 0.01f
            ? Mathf.Clamp01(m_LateWallSurgeTimer / lateWallSpeedBoostDuration)
            : 0f;
        float postWallMultiplier = Mathf.Lerp(1.0f, lateWallSpeedBoostMultiplier, Mathf.SmoothStep(0f, 1f, surgeProgress));

        return Mathf.Max(inWallMultiplier, postWallMultiplier);
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

            m_LastOverlappedWallCollider = col;
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
        isCloakedWhileFleeing = false;
        if (m_GhostTarget != null)
        {
            m_GhostTarget.SetCloakCaptureImmunity(false);
        }

        m_StalkTimer = 0f;
        m_BaseYPosition = transform.position.y;
        m_LastTraceDropPosition = transform.position;
        m_PreviousChestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;
        Debug.Log($"[{gameObject.name}] Player hasn't found ghost for {stalkStartDelaySeconds:F0}s -> Entering STALKING Phantom state (BLUE)!");
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
            float activeStalkSpeed = stalkSpeed * GetActiveWallSpeedMultiplier();
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
        bool effectivelyIlluminated = m_GhostTarget != null && m_GhostTarget.IsIlluminated && !isCloakedWhileFleeing;
        float captureProgress = (m_GhostTarget != null && !isCloakedWhileFleeing) ? m_GhostTarget.CurrentProgressNormalized : 0f;

        // 1. Reactive Flashlight Panic + Stage 2 Beam-Lock Slowdown! (Normal Flashlight does nothing while cloaked-fleeing > 1.5m!)
        float activeSpeed = fleeSpeed;
        float activeWeaveAmp = weaveAmplitude;

        if (enablePanicDodge && effectivelyIlluminated)
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

        activeSpeed *= GetActiveWallSpeedMultiplier();

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
                && !isCloakedWhileFleeing
                && m_GhostTarget != null
                && m_GhostTarget.IsRecentlyIlluminated;

            if (heldInFlashlightBeam && hidingSpots.Count > 1)
            {
                m_PreviousSpot = m_CurrentSpot;
                m_CurrentSpot = m_TargetSpot;
                Debug.Log($"[{gameObject.name}] Reached '{m_CurrentSpot.SpotName}' while illuminated! Beam Burn-Lock prevents cloaking — bolting to a new spot!");
                StartFleeing(allowCloakedEscape: false);
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
            isCloakedWhileFleeing = false;
            if (m_GhostTarget != null)
            {
                m_GhostTarget.SetCloakCaptureImmunity(false);
            }
            currentState = GhostState.Hidden;
            Debug.Log($"[{gameObject.name}] Ghost safely reached hiding spot '{m_CurrentSpot.SpotName}' — dematerializing into cloak.");
        }
    }

    // =========================================================================
    // STAGE 2: BEAM STRUGGLE VIBRATION & HOVER HELPERS
    // =========================================================================
    private Vector3 CalculateBeamStruggleOffset()
    {
        if (!enableBeamStruggle || isCloakedWhileFleeing || m_GhostTarget == null || !m_GhostTarget.IsIlluminated)
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
                        GetActiveTraceUVColor()
                    );
                }
            }
        }

        Debug.Log($"[{gameObject.name}] *** SEARCH HINT EMITTED! *** Brief spectral shimmer + {hintBreadcrumbCount} UV breadcrumb rings spawned toward '{(m_CurrentSpot != null ? m_CurrentSpot.SpotName : "Ghost")}'.");
    }

    // =========================================================================
    // FAKE ESCAPE, ROUND RESET & TACTICAL HIDING SPOT SCORING
    // =========================================================================
    /// <summary>
    /// Play-Mode testing shortcut: right-click GhostAI and choose this to guarantee the very next escape is Invisible!
    /// </summary>
    [ContextMenu("Force Next Escape Invisible (Test)")]
    public void ForceNextEscapeInvisible()
    {
        m_ForceNextEscapeCloaked = true;
        Debug.Log($"[{gameObject.name}] Next escape is armed to 100% INVISIBLE FLEE (Purple UV traces + {cloakedFleeRevealDistance:F1}m flashlight break)!");
    }

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

        bool chooseWatch = !m_ForceNextEscapeCloaked && (Random.value <= watchPlayerChance);

        if (chooseWatch)
        {
            isCloakedWhileFleeing = false;
            if (m_GhostTarget != null)
            {
                m_GhostTarget.SetCloakCaptureImmunity(false);
            }

            currentState = GhostState.WatchingPlayer;
            m_WatchTimer = watchPlayerDuration;
            m_WasIlluminatedDuringStare = false;
            m_StareBeamLostGraceTimer = stareBeamLossGraceSeconds;
            Debug.Log($"[{gameObject.name}] {reason} -> Materializing (BLUE) into Fake Escape [Behaviour A: Staring for {watchPlayerDuration:F1}s]");
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
        float newWeaveFrequency = -1f,
        int roundNumber = -1)
    {
        if (m_GhostTarget == null)
        {
            m_GhostTarget = GetComponent<GhostTarget>();
        }

        if (roundNumber >= 1)
        {
            activeRoundNumber = roundNumber;
        }
        else if (RoundManager.Instance != null)
        {
            activeRoundNumber = RoundManager.Instance.CurrentRound;
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
        m_LateWallSurgeTimer = 0f;
        m_WasPhasingLastFrame = false;
        m_WasIlluminatedDuringStare = false;
        isCloakedWhileFleeing = false;
        m_ForceNextEscapeCloaked = false;
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
            m_GhostTarget.SetSpectralState(
                currentSpectralVisibility,
                false,
                cloakWhileHidden ? uvShimmerRimColor : exposedVisibleBlueColor,
                cloakWhileHidden ? 0f : 1.45f
            );
        }
        else
        {
            gameObject.SetActive(true);
        }

        m_PreviousChestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : transform.position;

        string wallTier = IsLateWallPhaseTier ? "LATE (Super-Fast Surge + Ghost Outline)" : "EARLY (Splash)";
        Debug.Log($"[{gameObject.name}] Round {activeRoundNumber} Spawn (Cloaked={cloakWhileHidden}, WallTier={wallTier}) at '{(m_CurrentSpot != null ? m_CurrentSpot.SpotName : "Start")}' | Speed: {fleeSpeed:F2} m/s");
    }

    private void StartFleeing(bool allowCloakedEscape = true)
    {
        Vector3 escapeOrigin = transform.position;
        m_TargetSpot = GetSmartNextHidingSpot();
        m_CorePathPosition = escapeOrigin;
        m_LastTraceDropPosition = escapeOrigin;
        m_PreviousChestCenter = m_GhostTarget != null ? m_GhostTarget.TargetCenter : escapeOrigin;
        m_CurrentLateralOffset = 0f;
        m_LateralOffsetVelocity = 0f;
        m_FleePhaseTime = 0f;
        m_FleeElapsedSeconds = 0f;
        m_TimeUnderBeamWhileFleeing = 0f;
        m_JukeSign = Random.value > 0.5f ? 1f : -1f;
        m_SmoothedJukeSign = m_JukeSign;
        m_JukeSignVelocity = 0f;
        m_NoiseSeed = Random.Range(0f, 100f);

        bool rollCloakedEscape = m_ForceNextEscapeCloaked ||
            (allowCloakedEscape && enableCloakedFlee && cloakWhileHidden && (Random.value < cloakedFleeChance));
        m_ForceNextEscapeCloaked = false;

        currentState = GhostState.Fleeing;
        isCloakedWhileFleeing = rollCloakedEscape;

        if (m_GhostTarget != null)
        {
            m_GhostTarget.SetCloakCaptureImmunity(isCloakedWhileFleeing);
        }

        if (isCloakedWhileFleeing && currentSpectralVisibility < 0.52f)
        {
            // Brief purple phantom flash so the player sees her slip into stealth as she bolts!
            currentSpectralVisibility = 0.55f;
        }

        if (m_TargetSpot != null)
        {
            Vector3 flatDir = m_TargetSpot.Position - m_CorePathPosition;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                m_LastPathForward = flatDir.normalized;
            }

            string modeDesc = isCloakedWhileFleeing
                ? $"STEALTH INVISIBLE FLEE (PURPLE UV traces | Normal Flashlight immune until <= {cloakedFleeRevealDistance:F1}m!)"
                : "VISIBLE FLEE (BLUE body + BLUE UV traces)";
            Debug.Log($"[{gameObject.name}] Ghost fleeing to '{m_TargetSpot.SpotName}' -> {modeDesc}");
        }

        // Stage 3: Leave a pulsing UV Residual Ring at the hiding spot she bolted from (Blue if visible, Purple if invisible!)
        SpawnEscapeOriginRing(escapeOrigin);
        UpdateTraceTrail(m_LastPathForward);
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

        isCloakedWhileFleeing = false;
        if (m_GhostTarget != null)
        {
            m_GhostTarget.SetCloakCaptureImmunity(false);
        }

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
