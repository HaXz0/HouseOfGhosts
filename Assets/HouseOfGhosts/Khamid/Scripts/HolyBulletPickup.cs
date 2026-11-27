using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class HolyBulletPickup : MonoBehaviour
{
    public enum PickupCollectionMode
    {
        InstantGrabOrTouch = 0,     // Option A (Active now): Grab with either hand or touch with pistol -> +1 ammo immediately
        PhysicalReloadToPistol = 1  // Option B (Future pass): Carry bullet in hand and touch it to pistol to load
    }

    [Header("Pickup Behavior")]
    [Tooltip("Option A (InstantGrabOrTouch) adds +1 ammo when grabbed or touched by the pistol. Ready to expand to Option B later.")]
    [SerializeField] private PickupCollectionMode collectionMode = PickupCollectionMode.InstantGrabOrTouch;

    [Tooltip("Ammo granted when collected.")]
    [Min(1)]
    [SerializeField] private int ammoAmount = 1;

    [Tooltip("If true, touching this bullet directly with the Holy Pistol collider also collects it.")]
    [SerializeField] private bool allowDirectPistolTouch = true;

    [Header("Bullet Visual (Easy 1-Slot Prefab Swap)")]
    [Tooltip("Drag PistolBulletAmmo.prefab now (or Ferron's bullet prefab later) here.")]
    [SerializeField] private GameObject visualModelPrefab;

    [Tooltip("Parent transform where visualModelPrefab is placed. Auto-created as 'VisualHolder' if empty.")]
    [SerializeField] private Transform visualHolder;

    [Tooltip("Removes colliders on the visual mesh so the comfortable 9cm VR grab SphereCollider handles pickup cleanly.")]
    [SerializeField] private bool stripVisualColliders = true;

    [Header("Subtle Idle Readability")]
    [Tooltip("Gentle slow hover/spin so small 3cm bullets stay easy to spot on dark horror floors.")]
    [SerializeField] private bool enableIdleHoverAndSpin = true;

    [SerializeField] private float hoverAmplitude = 0.022f;
    [SerializeField] private float hoverFrequency = 2.0f;
    [SerializeField] private float spinDegreesPerSecond = 55.0f;

    [Header("Haptics & Events (Ready for Future Pickup Sound / VFX)")]
    [Range(0f, 1f)]
    [SerializeField] private float pickupHapticAmplitude = 0.35f;
    [SerializeField] private float pickupHapticDuration = 0.08f;

    public UnityEvent onBulletCollected;

    [SerializeField, HideInInspector] private GameObject spawnedVisualSourcePrefab;

    private XRSimpleInteractable m_Interactable;
    private HolyBulletSpawner m_OwnerSpawner;
    private HolyBulletSpawnZone m_SourceZone;
    private Vector3 m_VisualRestLocalPos = Vector3.zero;
    private float m_HoverPhase;
    private bool m_IsCollected = false;
    private float m_LastFullNoticeTime = -5f;

    public HolyBulletSpawnZone SourceZone => m_SourceZone;
    public bool IsCollected => m_IsCollected;

    private void Awake()
    {
        m_Interactable = GetComponent<XRSimpleInteractable>();
        m_HoverPhase = Random.Range(0f, Mathf.PI * 2f);

        SphereCollider triggerCol = GetComponent<SphereCollider>();
        if (triggerCol != null)
        {
            triggerCol.isTrigger = true;
        }

        EnsureVisualHolderAndModel();
        if (visualHolder != null)
        {
            m_VisualRestLocalPos = visualHolder.localPosition;
        }
    }

    private void OnEnable()
    {
        if (m_Interactable != null)
        {
            m_Interactable.selectEntered.AddListener(OnSelectedByHand);
        }
    }

    private void OnDisable()
    {
        if (m_Interactable != null)
        {
            m_Interactable.selectEntered.RemoveListener(OnSelectedByHand);
        }
    }

    private void Update()
    {
        if (m_IsCollected || !enableIdleHoverAndSpin || visualHolder == null)
            return;

        float bobY = Mathf.Sin(Time.time * hoverFrequency + m_HoverPhase) * hoverAmplitude;
        visualHolder.localPosition = m_VisualRestLocalPos + new Vector3(0f, bobY, 0f);
        visualHolder.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }

    public void InitializeFromSpawner(HolyBulletSpawner spawner, HolyBulletSpawnZone zone)
    {
        m_OwnerSpawner = spawner;
        m_SourceZone = zone;
        m_IsCollected = false;
    }

    private void OnSelectedByHand(SelectEnterEventArgs args)
    {
        if (m_IsCollected || collectionMode != PickupCollectionMode.InstantGrabOrTouch)
            return;

        TryCollectForPistol(args.interactorObject, null);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_IsCollected || other == null)
            return;

        if (!allowDirectPistolTouch && collectionMode == PickupCollectionMode.InstantGrabOrTouch)
            return;

        HolyPistolController touchedPistol = other.GetComponentInParent<HolyPistolController>();
        if (touchedPistol != null)
        {
            TryCollectForPistol(null, touchedPistol);
        }
    }

    /// <summary>
    /// Attempts to collect this Holy Bullet into the player's Holy Pistol.
    /// If the pistol is already at max held ammo (3/3), keeps the bullet in the world untouched!
    /// </summary>
    public bool TryCollectForPistol(IXRInteractor grabbingInteractor = null, HolyPistolController explicitPistol = null)
    {
        if (m_IsCollected)
            return false;

        HolyPistolController pistol = explicitPistol != null
            ? explicitPistol
            : (HolyPistolController.Instance != null ? HolyPistolController.Instance : FindFirstObjectByType<HolyPistolController>());

        if (pistol == null)
        {
            Debug.LogWarning("[HolyBulletPickup] Could not find HolyPistolController in scene to receive ammo!", this);
            return false;
        }

        if (!pistol.TryAddAmmo(ammoAmount))
        {
            if (Time.time - m_LastFullNoticeTime > 1.2f)
            {
                m_LastFullNoticeTime = Time.time;
                Debug.Log($"[HolyBulletPickup] Pistol ammo full ({pistol.CurrentAmmo}/{pistol.MaxAmmo}) — bullet left in world.");
            }
            return false;
        }

        m_IsCollected = true;

        if (grabbingInteractor is Component interactorComp)
        {
            HapticImpulsePlayer haptic = interactorComp.GetComponentInParent<HapticImpulsePlayer>();
            if (haptic != null)
            {
                haptic.SendHapticImpulse(pickupHapticAmplitude, pickupHapticDuration);
            }
        }

        Vector3 collectedPos = transform.position;
        onBulletCollected?.Invoke();

        if (m_OwnerSpawner != null)
        {
            m_OwnerSpawner.NotifyBulletCollected(this, m_SourceZone, collectedPos);
        }

        Destroy(gameObject);
        return true;
    }

    private void EnsureVisualHolderAndModel()
    {
        if (visualHolder == null)
        {
            Transform existing = transform.Find("VisualHolder");
            if (existing != null)
            {
                visualHolder = existing;
            }
            else
            {
                GameObject holderObj = new GameObject("VisualHolder");
                visualHolder = holderObj.transform;
                visualHolder.SetParent(transform, false);
                visualHolder.localPosition = Vector3.zero;
                visualHolder.localRotation = Quaternion.identity;
                visualHolder.localScale = Vector3.one;
            }
        }

        if (visualModelPrefab != null && (visualHolder.childCount == 0 || spawnedVisualSourcePrefab != visualModelPrefab))
        {
            RebuildVisualModel();
        }
    }

    [ContextMenu("Rebuild Bullet Visual Now")]
    public void RebuildVisualModel()
    {
        if (visualHolder == null)
        {
            Transform existing = transform.Find("VisualHolder");
            if (existing == null)
            {
                GameObject holderObj = new GameObject("VisualHolder");
                visualHolder = holderObj.transform;
                visualHolder.SetParent(transform, false);
            }
            else
            {
                visualHolder = existing;
            }
        }

        for (int i = visualHolder.childCount - 1; i >= 0; i--)
        {
            GameObject child = visualHolder.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        spawnedVisualSourcePrefab = visualModelPrefab;
        if (visualModelPrefab == null)
            return;

        GameObject spawned = Instantiate(visualModelPrefab, visualHolder, false);
        spawned.name = visualModelPrefab.name + "_Visual";
        spawned.transform.localPosition = Vector3.zero;

        if (stripVisualColliders)
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
    }
}
