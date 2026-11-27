using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class HolyPistolController : MonoBehaviour
{
    public static HolyPistolController Instance { get; private set; }

    [Header("Weapon Model (Easy 1-Slot Prefab Swap)")]
    [Tooltip("Drag your pistol visual prefab here (e.g. GunModel.prefab now -> Ferron's Glock19.prefab later).")]
    [SerializeField] private GameObject visualModelPrefab;

    [Tooltip("Parent transform where visualModelPrefab is spawned. Auto-created as 'ModelHolder' if left empty.")]
    [SerializeField] private Transform modelHolder;

    [Tooltip("Fine position offset applied to the spawned model inside ModelHolder.")]
    [SerializeField] private Vector3 modelPositionOffset = Vector3.zero;

    [Tooltip("Rotation offset applied to align the model's barrel with +Z (use (0,0,0) for GunModel, (0,90,0) for Ferron's Glock19).")]
    [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

    [Tooltip("Scale applied to the spawned visual model.")]
    [SerializeField] private Vector3 modelScale = Vector3.one;

    [Tooltip("Automatically centers combined renderer bounds onto ModelHolder (fixes prefabs whose meshes are offset from root, like GunModel.prefab).")]
    [SerializeField] private bool autoCenterModelBounds = true;

    [Tooltip("Automatically removes colliders on visual child meshes so grabbing & physics use one clean root BoxCollider.")]
    [SerializeField] private bool stripVisualModelColliders = true;

    [Tooltip("Automatically fits the root BoxCollider around the centered visual model bounds.")]
    [SerializeField] private bool autoFitRootColliderToModel = true;

    [Tooltip("Automatically aligns MuzzlePoint to the front-top barrel tip of the swapped model (unless the model already has a child named 'MuzzlePoint').")]
    [SerializeField] private bool autoAlignMuzzleToModel = true;

    [Header("Aim & Grip Transforms")]
    [Tooltip("Origin & +Z forward direction of Holy Pistol shots. Auto-created if left empty.")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("VR hand attach point for XRGrabInteractable. Auto-created as 'GripAttach' if left empty.")]
    [SerializeField] private Transform gripAttachPoint;

    [Header("Holy Pistol Ammo & Cooldown")]
    [Tooltip("Bullets loaded in the pistol at game start.")]
    [Min(0)]
    [SerializeField] private int startingAmmo = 2;

    [Tooltip("Maximum bullets the pistol can hold at once (held cap = 3).")]
    [Min(1)]
    [SerializeField] private int maxAmmoCapacity = 3;

    [Tooltip("Moderate cooldown (seconds) between shots so missing is tense and the flashlight stays important.")]
    [Range(0.25f, 10.0f)]
    [SerializeField] private float fireCooldownSeconds = 3.0f;

    [Header("Hit Detection")]
    [Tooltip("Maximum shot distance in meters.")]
    [Range(2.0f, 40.0f)]
    [SerializeField] private float shotRange = 18.0f;

    [Tooltip("Forgiving VR aim assist sphere radius (meters). Strictly blocked by solid walls so you can never shoot through cover.")]
    [Range(0.0f, 0.25f)]
    [SerializeField] private float hitAssistRadius = 0.09f;

    [Tooltip("Layers hit by Holy Pistol shots (Ghost + Walls/Obstacles).")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Fired Bullet Visual (Pooled 3D Mesh — No VFX)")]
    [Tooltip("If true, launches a visible pooled 3D bullet mesh from MuzzlePoint to the impact point on every shot.")]
    [SerializeField] private bool showFiredBulletVisual = true;

    [Tooltip("3D bullet prefab launched visually on shot (e.g. Bullet (1).prefab).")]
    [SerializeField] private GameObject firedBulletVisualPrefab;

    [Tooltip("Local rotation aligning the bullet prefab's pointed nose with +Z (use (0,90,0) for Bullet (1).prefab).")]
    [SerializeField] private Vector3 firedBulletRotationOffset = new Vector3(0f, 90f, 0f);

    [Tooltip("Visual scale multiplier for the flying bullet (1.6x keeps a 9mm bullet clearly visible in VR flight).")]
    [Range(0.5f, 4.0f)]
    [SerializeField] private float firedBulletScale = 1.6f;

    [Tooltip("Flight speed (meters/second) of the visible bullet mesh from muzzle to hit point.")]
    [Range(8.0f, 80.0f)]
    [SerializeField] private float firedBulletSpeed = 30.0f;

    [Header("VR Feel & Testing")]
    [Tooltip("Subtle visual kickback on ModelHolder when firing (never moves the player's VR camera).")]
    [SerializeField] private bool enableVisualRecoil = true;

    [SerializeField] private float recoilKickBackMeters = 0.028f;
    [SerializeField] private float recoilKickUpDegrees = 9.0f;
    [SerializeField] private float recoilRecoverySpeed = 12.0f;

    [Range(0f, 1f)]
    [SerializeField] private float shotHapticAmplitude = 0.72f;
    [SerializeField] private float shotHapticDuration = 0.11f;

    [Range(0f, 1f)]
    [SerializeField] private float dryFireHapticAmplitude = 0.20f;
    [SerializeField] private float dryFireHapticDuration = 0.05f;

    [Tooltip("Allows pressing 'F' on the PC keyboard in Play Mode to test firing without a VR controller.")]
    [SerializeField] private bool enableKeyboardFTestFire = true;

    [Header("Current State (Read Only)")]
    [SerializeField] private int currentAmmo = 2;
    [SerializeField] private float cooldownRemaining = 0f;
    [SerializeField] private bool isReadyToFire = true;

    [Header("Events (Ready for Future Cross VFX, Audio & UI)")]
    [Tooltip("Invoked when a shot fires. Passes (muzzleWorldPos, hitWorldPos, didBanishGhost).")]
    public UnityEvent<Vector3, Vector3, bool> onShotFired;

    [Tooltip("Invoked specifically when a shot banishes a ghost or test dummy. Passes hitWorldPos.")]
    public UnityEvent<Vector3> onGhostBanished;

    [Tooltip("Invoked if a shot hits an unexposed/cloaked ghost (flashlight exposure required first).")]
    public UnityEvent onShotBlockedByCloak;

    [Tooltip("Invoked when the trigger is pulled with 0 ammo or while cooling down.")]
    public UnityEvent onDryFire;

    [Tooltip("Invoked once when the shot cooldown finishes and the pistol is ready again.")]
    public UnityEvent onCooldownReady;

    [Tooltip("Invoked whenever ammo changes. Passes (currentAmmo, maxAmmoCapacity).")]
    public UnityEvent<int, int> onAmmoChanged;

    [Tooltip("Invoked while cooling down and when ready. Passes remaining cooldown normalized (1 = just fired, 0 = ready).")]
    public UnityEvent<float> onCooldownNormalizedChanged;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool drawDebugGizmos = true;

    [SerializeField, HideInInspector] private GameObject spawnedModelSourcePrefab;

    private XRGrabInteractable m_GrabInteractable;
    private Rigidbody m_Rigidbody;
    private FlashlightRecoveryHighlight m_RecoveryHighlight;
    private Collider[] m_SelfColliders;
    private readonly RaycastHit[] m_HitBuffer = new RaycastHit[16];

    private Vector3 m_ModelHolderRestLocalPos;
    private Quaternion m_ModelHolderRestLocalRot = Quaternion.identity;
    private float m_CurrentRecoilBlend = 0f;
    private bool m_WasCoolingDown = false;
    private Vector3 m_LastShotStart;
    private Vector3 m_LastShotEnd;
    private float m_LastShotDebugTimer = 0f;

    private struct FlyingBulletSlot
    {
        public Transform Root;
        public bool Active;
        public Vector3 Direction;
        public float RemainingDistance;
    }

    private const int k_FlyingBulletPoolSize = 3;
    private Transform m_FlyingBulletPoolContainer;
    private FlyingBulletSlot[] m_FlyingBulletPool;
    private int m_NextFlyingBulletIndex = 0;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmoCapacity;
    public bool IsAmmoFull => currentAmmo >= maxAmmoCapacity;
    public float CooldownRemaining => cooldownRemaining;
    public float CooldownNormalized => fireCooldownSeconds > 0.01f ? Mathf.Clamp01(cooldownRemaining / fireCooldownSeconds) : 0f;
    public bool CanFire => currentAmmo > 0 && cooldownRemaining <= 0.001f;
    public Transform MuzzlePoint => muzzlePoint != null ? muzzlePoint : transform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HolyPistolController] Multiple HolyPistolController instances found in scene.", this);
        }
        Instance = this;

        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_RecoveryHighlight = GetComponent<FlashlightRecoveryHighlight>();

        if (m_Rigidbody != null)
        {
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        EnsureHierarchyAnchors();
        EnsureVisualModelUpToDate();

        m_ModelHolderRestLocalPos = modelHolder != null ? modelHolder.localPosition : Vector3.zero;
        m_ModelHolderRestLocalRot = modelHolder != null ? modelHolder.localRotation : Quaternion.identity;

        currentAmmo = Mathf.Clamp(startingAmmo, 0, maxAmmoCapacity);
        cooldownRemaining = 0f;
        isReadyToFire = currentAmmo > 0;
        m_WasCoolingDown = false;

        m_SelfColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        InitializeFlyingBulletPool();
        onAmmoChanged?.Invoke(currentAmmo, maxAmmoCapacity);
        onCooldownNormalizedChanged?.Invoke(0f);
    }

    private void OnEnable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.activated.AddListener(OnTriggerActivated);
            m_GrabInteractable.selectEntered.AddListener(OnGrabbed);
        }
    }

    private void OnDisable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.activated.RemoveListener(OnTriggerActivated);
            m_GrabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }
    }

    private void OnDestroy()
    {
        if (m_FlyingBulletPoolContainer != null)
        {
            Destroy(m_FlyingBulletPoolContainer.gameObject);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            m_WasCoolingDown = true;
            onCooldownNormalizedChanged?.Invoke(CooldownNormalized);

            if (cooldownRemaining <= 0f)
            {
                m_WasCoolingDown = false;
                if (showDebugLogs)
                {
                    Debug.Log($"[{gameObject.name}] Holy Pistol cooldown finished — ready to fire! (Ammo: {currentAmmo}/{maxAmmoCapacity})");
                }
                onCooldownReady?.Invoke();
            }
        }
        else if (m_WasCoolingDown)
        {
            m_WasCoolingDown = false;
            onCooldownNormalizedChanged?.Invoke(0f);
        }

        isReadyToFire = CanFire;

        if (enableVisualRecoil && modelHolder != null && m_CurrentRecoilBlend > 0.0001f)
        {
            m_CurrentRecoilBlend = Mathf.MoveTowards(m_CurrentRecoilBlend, 0f, recoilRecoverySpeed * Time.deltaTime);
            ApplyVisualRecoilTransform(m_CurrentRecoilBlend);
        }

        if (m_LastShotDebugTimer > 0f)
        {
            m_LastShotDebugTimer = Mathf.Max(0f, m_LastShotDebugTimer - Time.deltaTime);
        }

        UpdateFlyingBullets();

        if (enableKeyboardFTestFire && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryFireShot(null);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            return;

        SendHapticToInteractor(args.interactorObject, 0.28f, 0.07f);
    }

    private void OnTriggerActivated(ActivateEventArgs args)
    {
        TryFireShot(args.interactorObject);
    }

    /// <summary>
    /// Attempts to add bullets to the Holy Pistol up to maxAmmoCapacity (held cap = 3).
    /// Returns false if already full so world bullet pickups are never wasted!
    /// </summary>
    public bool TryAddAmmo(int amount = 1)
    {
        if (amount <= 0)
            return false;

        if (currentAmmo >= maxAmmoCapacity)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[{gameObject.name}] Holy Pistol ammo already FULL ({currentAmmo}/{maxAmmoCapacity}) — pickup kept on floor.");
            }
            return false;
        }

        int previous = currentAmmo;
        currentAmmo = Mathf.Min(maxAmmoCapacity, currentAmmo + amount);
        isReadyToFire = CanFire;

        if (showDebugLogs)
        {
            Debug.Log($"[{gameObject.name}] Collected Holy Bullet! Ammo: {previous} -> {currentAmmo}/{maxAmmoCapacity}");
        }

        onAmmoChanged?.Invoke(currentAmmo, maxAmmoCapacity);
        return true;
    }

    /// <summary>
    /// Swaps the visual weapon prefab cleanly at runtime and refreshes lost-item X-Ray overlays.
    /// </summary>
    public void SetVisualModelPrefab(GameObject newModelPrefab, Vector3 newRotationOffset)
    {
        visualModelPrefab = newModelPrefab;
        modelRotationOffset = newRotationOffset;
        RebuildVisualModel();
    }

    [ContextMenu("Fire Holy Shot (Test)")]
    public void FireShotFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] Enter Play Mode first to test firing the Holy Pistol.");
            return;
        }

        TryFireShot(null);
    }

    [ContextMenu("Refill Ammo To Max (Test)")]
    public void RefillAmmoToMax()
    {
        currentAmmo = maxAmmoCapacity;
        cooldownRemaining = 0f;
        isReadyToFire = true;
        onAmmoChanged?.Invoke(currentAmmo, maxAmmoCapacity);
        onCooldownNormalizedChanged?.Invoke(0f);

        if (showDebugLogs)
        {
            Debug.Log($"[{gameObject.name}] Holy Pistol refilled to {currentAmmo}/{maxAmmoCapacity} with cooldown cleared.");
        }
    }

    /// <summary>
    /// Attempts to fire one Holy Pistol shot. Respects ammo count (2/3 start, max 3/3) and moderate cooldown (3.0s).
    /// </summary>
    public bool TryFireShot(IXRInteractor firingInteractor = null)
    {
        if (cooldownRemaining > 0.001f)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[{gameObject.name}] Holy Pistol recharging ({cooldownRemaining:F1}s left).");
            }
            SendHapticToInteractor(firingInteractor, dryFireHapticAmplitude, dryFireHapticDuration);
            onDryFire?.Invoke();
            return false;
        }

        if (currentAmmo <= 0)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[{gameObject.name}] CLICK — Out of Holy Bullets (0/{maxAmmoCapacity})! Find bullets around the map.");
            }
            SendHapticToInteractor(firingInteractor, dryFireHapticAmplitude, dryFireHapticDuration);
            onDryFire?.Invoke();
            return false;
        }

        currentAmmo--;
        cooldownRemaining = fireCooldownSeconds;
        isReadyToFire = false;
        m_WasCoolingDown = true;

        Transform muzzle = MuzzlePoint;
        Vector3 origin = muzzle.position;
        Vector3 direction = muzzle.forward;

        Vector3 hitPoint = origin + direction * shotRange;
        bool didBanishGhost = EvaluateShotHit(origin, direction, ref hitPoint, out bool blockedByCloak);

        m_LastShotStart = origin;
        m_LastShotEnd = hitPoint;
        m_LastShotDebugTimer = 1.5f;

        LaunchFiredBulletVisual(origin, hitPoint);

        if (enableVisualRecoil && modelHolder != null)
        {
            m_CurrentRecoilBlend = 1.0f;
            ApplyVisualRecoilTransform(1.0f);
        }

        SendHapticToInteractor(firingInteractor, shotHapticAmplitude, shotHapticDuration);

        if (showDebugLogs)
        {
            string hitSummary = didBanishGhost
                ? "DIRECT GHOST BANISHMENT!"
                : (blockedByCloak ? "PASSED THROUGH CLOAKED GHOST (expose with flashlight first!)" : "Missed / hit environment.");
            Debug.Log($"[{gameObject.name}] *** HOLY PISTOL FIRED! *** {hitSummary} | Ammo left: {currentAmmo}/{maxAmmoCapacity} | Cooldown: {fireCooldownSeconds:F1}s");
        }

        onAmmoChanged?.Invoke(currentAmmo, maxAmmoCapacity);
        onCooldownNormalizedChanged?.Invoke(1.0f);
        onShotFired?.Invoke(origin, hitPoint, didBanishGhost);

        if (didBanishGhost)
        {
            onGhostBanished?.Invoke(hitPoint);
        }
        else if (blockedByCloak)
        {
            onShotBlockedByCloak?.Invoke();
        }

        return true;
    }

    private bool EvaluateShotHit(Vector3 origin, Vector3 direction, ref Vector3 hitPoint, out bool blockedByCloak)
    {
        blockedByCloak = false;

        // 1. Strict center raycast first to establish exact obstacle/wall blocking distance
        float maxCheckDistance = shotRange;
        GhostTarget directGhostHit = null;
        Vector3 directGhostPoint = hitPoint;
        float closestWallDist = shotRange;
        Vector3 closestWallPoint = origin + direction * shotRange;

        int rayCount = Physics.RaycastNonAlloc(origin, direction, m_HitBuffer, shotRange, hitLayers, QueryTriggerInteraction.Ignore);
        float closestAnyDist = float.MaxValue;

        for (int i = 0; i < rayCount; i++)
        {
            RaycastHit hit = m_HitBuffer[i];
            if (IsIgnoredShotCollider(hit.collider))
                continue;

            GhostTarget target = hit.collider.GetComponentInParent<GhostTarget>();
            if (target != null)
            {
                if (hit.distance < closestAnyDist)
                {
                    closestAnyDist = hit.distance;
                    directGhostHit = target;
                    directGhostPoint = hit.point;
                }
            }
            else if (hit.distance < closestWallDist)
            {
                closestWallDist = hit.distance;
                closestWallPoint = hit.point;
                if (hit.distance < closestAnyDist)
                {
                    closestAnyDist = hit.distance;
                    directGhostHit = null;
                }
            }
        }

        if (directGhostHit != null && closestAnyDist <= closestWallDist)
        {
            hitPoint = directGhostPoint;
            if (directGhostHit.CaptureByHolyPistol(hitPoint, direction))
                return true;

            blockedByCloak = !directGhostHit.IsCaptured;
            return false;
        }

        hitPoint = closestWallPoint;
        maxCheckDistance = closestWallDist;

        // 2. Forgiving VR aim SphereCast strictly up to the closest wall distance (never shoots through cover!)
        if (hitAssistRadius > 0.005f && maxCheckDistance > 0.05f)
        {
            int sphereCount = Physics.SphereCastNonAlloc(origin, hitAssistRadius, direction, m_HitBuffer, maxCheckDistance, hitLayers, QueryTriggerInteraction.Ignore);
            GhostTarget bestGhost = null;
            float bestDist = float.MaxValue;
            Vector3 bestPoint = hitPoint;

            for (int i = 0; i < sphereCount; i++)
            {
                RaycastHit hit = m_HitBuffer[i];
                if (IsIgnoredShotCollider(hit.collider))
                    continue;

                GhostTarget target = hit.collider.GetComponentInParent<GhostTarget>();
                if (target == null || target.IsCaptured)
                    continue;

                if (hit.distance < bestDist && HasClearLineToPoint(origin, hit.point, target))
                {
                    bestDist = hit.distance;
                    bestGhost = target;
                    bestPoint = hit.point;
                }
            }

            if (bestGhost != null)
            {
                hitPoint = bestPoint;
                if (bestGhost.CaptureByHolyPistol(hitPoint, direction))
                    return true;

                blockedByCloak = !bestGhost.IsCaptured;
                return false;
            }
        }

        return false;
    }

    private bool HasClearLineToPoint(Vector3 origin, Vector3 targetPoint, GhostTarget expectedTarget)
    {
        Vector3 delta = targetPoint - origin;
        float dist = delta.magnitude;
        if (dist <= 0.05f)
            return true;

        int hits = Physics.RaycastNonAlloc(origin, delta / dist, m_HitBuffer, dist, hitLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider col = m_HitBuffer[i].collider;
            if (IsIgnoredShotCollider(col))
                continue;

            GhostTarget hitTarget = col.GetComponentInParent<GhostTarget>();
            if (hitTarget == expectedTarget)
                continue;

            if (m_HitBuffer[i].distance < dist - 0.06f)
                return false;
        }

        return true;
    }

    private bool IsIgnoredShotCollider(Collider col)
    {
        if (col == null)
            return true;

        if (m_SelfColliders != null)
        {
            for (int i = 0; i < m_SelfColliders.Length; i++)
            {
                if (m_SelfColliders[i] == col)
                    return true;
            }
        }

        if (col.GetComponentInParent<HolyBulletPickup>() != null)
            return true;

        return false;
    }

    private void ApplyVisualRecoilTransform(float recoil01)
    {
        if (modelHolder == null)
            return;

        float eased = recoil01 * recoil01;
        modelHolder.localPosition = m_ModelHolderRestLocalPos + new Vector3(0f, 0f, -recoilKickBackMeters * eased);
        modelHolder.localRotation = m_ModelHolderRestLocalRot * Quaternion.Euler(-recoilKickUpDegrees * eased, 0f, 0f);
    }

    private void InitializeFlyingBulletPool()
    {
        if (!showFiredBulletVisual || firedBulletVisualPrefab == null || m_FlyingBulletPool != null)
            return;

        GameObject containerObj = new GameObject($"{gameObject.name}_FiredBullets_Pool");
        m_FlyingBulletPoolContainer = containerObj.transform;
        m_FlyingBulletPool = new FlyingBulletSlot[k_FlyingBulletPoolSize];

        for (int i = 0; i < k_FlyingBulletPoolSize; i++)
        {
            GameObject slotRoot = new GameObject($"FiredBullet_{i}");
            slotRoot.transform.SetParent(m_FlyingBulletPoolContainer, false);

            GameObject meshObj = Instantiate(firedBulletVisualPrefab, slotRoot.transform, false);
            meshObj.name = "BulletMesh";
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localRotation = Quaternion.Euler(firedBulletRotationOffset);
            meshObj.transform.localScale = Vector3.one * firedBulletScale;

            Collider[] cols = meshObj.GetComponentsInChildren<Collider>(true);
            for (int c = cols.Length - 1; c >= 0; c--)
            {
                if (cols[c] != null)
                    Destroy(cols[c]);
            }

            slotRoot.SetActive(false);
            m_FlyingBulletPool[i] = new FlyingBulletSlot
            {
                Root = slotRoot.transform,
                Active = false,
                Direction = Vector3.forward,
                RemainingDistance = 0f
            };
        }
    }

    private void LaunchFiredBulletVisual(Vector3 startPos, Vector3 endPos)
    {
        if (!showFiredBulletVisual || firedBulletVisualPrefab == null)
            return;

        if (m_FlyingBulletPool == null)
        {
            InitializeFlyingBulletPool();
            if (m_FlyingBulletPool == null)
                return;
        }

        Vector3 delta = endPos - startPos;
        float totalDistance = delta.magnitude;
        if (totalDistance < 0.05f)
            return;

        Vector3 dir = delta / totalDistance;
        int idx = m_NextFlyingBulletIndex;
        m_NextFlyingBulletIndex = (m_NextFlyingBulletIndex + 1) % m_FlyingBulletPool.Length;

        FlyingBulletSlot slot = m_FlyingBulletPool[idx];
        if (slot.Root == null)
            return;

        slot.Root.position = startPos;
        slot.Root.rotation = Quaternion.LookRotation(dir, Vector3.up);
        slot.Root.gameObject.SetActive(true);
        slot.Active = true;
        slot.Direction = dir;
        slot.RemainingDistance = totalDistance;
        m_FlyingBulletPool[idx] = slot;
    }

    private void UpdateFlyingBullets()
    {
        if (m_FlyingBulletPool == null)
            return;

        float step = Mathf.Max(1.0f, firedBulletSpeed) * Time.deltaTime;
        for (int i = 0; i < m_FlyingBulletPool.Length; i++)
        {
            FlyingBulletSlot slot = m_FlyingBulletPool[i];
            if (!slot.Active || slot.Root == null)
                continue;

            if (slot.RemainingDistance <= step)
            {
                slot.Root.position += slot.Direction * slot.RemainingDistance;
                slot.Root.gameObject.SetActive(false);
                slot.Active = false;
                slot.RemainingDistance = 0f;
            }
            else
            {
                slot.Root.position += slot.Direction * step;
                slot.RemainingDistance -= step;
            }

            m_FlyingBulletPool[i] = slot;
        }
    }

    private void SendHapticToInteractor(IXRInteractor interactor, float amplitude, float duration)
    {
        if (interactor == null && m_GrabInteractable != null && m_GrabInteractable.interactorsSelecting.Count > 0)
        {
            for (int i = 0; i < m_GrabInteractable.interactorsSelecting.Count; i++)
            {
                if (!(m_GrabInteractable.interactorsSelecting[i] is XRSocketInteractor))
                {
                    interactor = m_GrabInteractable.interactorsSelecting[i];
                    break;
                }
            }
        }

        Component interactorComp = interactor as Component;
        if (interactorComp == null)
            return;

        HapticImpulsePlayer hapticPlayer = interactorComp.GetComponentInParent<HapticImpulsePlayer>();
        if (hapticPlayer != null)
        {
            hapticPlayer.SendHapticImpulse(amplitude, duration);
        }
    }

    // =========================================================================
    // EASY 1-SLOT VISUAL MODEL PREFAB SWAP (GunModel.prefab -> Glock19.prefab)
    // =========================================================================
    private void EnsureHierarchyAnchors()
    {
        if (modelHolder == null)
        {
            Transform existing = transform.Find("ModelHolder");
            if (existing != null)
            {
                modelHolder = existing;
            }
            else
            {
                GameObject holderObj = new GameObject("ModelHolder");
                modelHolder = holderObj.transform;
                modelHolder.SetParent(transform, false);
                modelHolder.localPosition = Vector3.zero;
                modelHolder.localRotation = Quaternion.identity;
                modelHolder.localScale = Vector3.one;
            }
        }

        if (gripAttachPoint == null)
        {
            Transform existing = transform.Find("GripAttach");
            if (existing != null)
            {
                gripAttachPoint = existing;
            }
            else
            {
                GameObject gripObj = new GameObject("GripAttach");
                gripAttachPoint = gripObj.transform;
                gripAttachPoint.SetParent(transform, false);
                gripAttachPoint.localPosition = new Vector3(0f, -0.025f, -0.03f);
                gripAttachPoint.localRotation = Quaternion.identity;
            }
        }

        if (m_GrabInteractable == null)
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
        }
        if (m_GrabInteractable != null && m_GrabInteractable.attachTransform == null)
        {
            m_GrabInteractable.attachTransform = gripAttachPoint;
        }

        if (muzzlePoint == null)
        {
            Transform existing = transform.Find("MuzzlePoint");
            if (existing != null)
            {
                muzzlePoint = existing;
            }
            else
            {
                GameObject muzzleObj = new GameObject("MuzzlePoint");
                muzzlePoint = muzzleObj.transform;
                muzzlePoint.SetParent(transform, false);
                muzzlePoint.localPosition = new Vector3(0f, 0.045f, 0.16f);
                muzzlePoint.localRotation = Quaternion.identity;
            }
        }
    }

    private void EnsureVisualModelUpToDate()
    {
        if (visualModelPrefab == null || modelHolder == null)
            return;

        if (modelHolder.childCount == 0 || spawnedModelSourcePrefab != visualModelPrefab)
        {
            RebuildVisualModel();
        }
    }

    /// <summary>
    /// Instantiates visualModelPrefab under ModelHolder, strips visual child colliders, auto-centers offset bounds,
    /// aligns MuzzlePoint & root BoxCollider, and rebuilds lost-item X-Ray overlays automatically!
    /// </summary>
    [ContextMenu("Rebuild Visual Model Now")]
    public void RebuildVisualModel()
    {
        EnsureHierarchyAnchors();

        if (modelHolder == null)
            return;

        for (int i = modelHolder.childCount - 1; i >= 0; i--)
        {
            GameObject child = modelHolder.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        spawnedModelSourcePrefab = visualModelPrefab;
        if (visualModelPrefab == null)
            return;

        GameObject spawned = Instantiate(visualModelPrefab, modelHolder, false);
        spawned.name = visualModelPrefab.name + "_Visual";
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.Euler(modelRotationOffset);
        spawned.transform.localScale = modelScale;

        if (stripVisualModelColliders)
        {
            Collider[] childCols = spawned.GetComponentsInChildren<Collider>(true);
            for (int i = childCols.Length - 1; i >= 0; i--)
            {
                if (childCols[i] == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(childCols[i]);
                else
                    DestroyImmediate(childCols[i]);
            }
        }

        // Compute combined renderer bounds in ModelHolder local space so offset prefabs (like GunModel's 8.37, 3.21, 59.74) center cleanly!
        Bounds localBounds = ComputeLocalRendererBounds(modelHolder, spawned.transform, out bool hasBounds);

        if (hasBounds && autoCenterModelBounds)
        {
            Vector3 centerOffset = -localBounds.center;
            spawned.transform.localPosition = centerOffset + modelPositionOffset;
            localBounds = ComputeLocalRendererBounds(modelHolder, spawned.transform, out hasBounds);
        }
        else
        {
            spawned.transform.localPosition = modelPositionOffset;
        }

        if (hasBounds)
        {
            if (autoFitRootColliderToModel)
            {
                BoxCollider rootBox = GetComponent<BoxCollider>();
                if (rootBox == null)
                    rootBox = gameObject.AddComponent<BoxCollider>();

                rootBox.center = localBounds.center;
                rootBox.size = new Vector3(
                    Mathf.Max(0.05f, localBounds.size.x + 0.015f),
                    Mathf.Max(0.08f, localBounds.size.y + 0.015f),
                    Mathf.Max(0.12f, localBounds.size.z + 0.015f)
                );
            }

            if (autoAlignMuzzleToModel && muzzlePoint != null)
            {
                Transform customMuzzle = FindNamedChild(spawned.transform, "MuzzlePoint");
                if (customMuzzle != null)
                {
                    muzzlePoint.position = customMuzzle.position;
                    muzzlePoint.rotation = customMuzzle.rotation;
                }
                else
                {
                    // Align muzzle cleanly at the front-upper barrel tip (+Z)
                    float barrelY = localBounds.max.y - (localBounds.size.y * 0.22f);
                    muzzlePoint.localPosition = new Vector3(localBounds.center.x, barrelY, localBounds.max.z + 0.012f);
                    muzzlePoint.localRotation = Quaternion.identity;
                }
            }
        }

        m_SelfColliders = GetComponentsInChildren<Collider>(true);

        if (Application.isPlaying && m_RecoveryHighlight != null)
        {
            m_RecoveryHighlight.RebuildXRayOverlays();
        }
    }

    private static Bounds ComputeLocalRendererBounds(Transform referenceSpace, Transform rootToMeasure, out bool foundAny)
    {
        foundAny = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] renderers = rootToMeasure.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r is ParticleSystemRenderer || r.gameObject.name.EndsWith("_XRayOverlay"))
                continue;

            Bounds wb = r.bounds;
            Vector3 c = wb.center;
            Vector3 e = wb.extents;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 worldCorner = new Vector3(c.x + sx * e.x, c.y + sy * e.y, c.z + sz * e.z);
                        Vector3 localCorner = referenceSpace.InverseTransformPoint(worldCorner);
                        if (!foundAny)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            foundAny = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        return localBounds;
    }

    private static Transform FindNamedChild(Transform parent, string targetName)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (string.Equals(children[i].name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return children[i];
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Transform muzzle = muzzlePoint != null ? muzzlePoint : transform;
        Gizmos.color = new Color(1.0f, 0.85f, 0.25f, 0.9f);
        Gizmos.DrawWireSphere(muzzle.position, 0.018f);
        Gizmos.DrawLine(muzzle.position, muzzle.position + muzzle.forward * 2.5f);

        if (m_LastShotDebugTimer > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(m_LastShotStart, m_LastShotEnd);
            Gizmos.DrawWireSphere(m_LastShotEnd, 0.08f);
        }
    }
}
