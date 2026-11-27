using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class HolyBulletSpawner : MonoBehaviour
{
    public static HolyBulletSpawner Instance { get; private set; }

    [Header("Bullet Prefab & Capacity")]
    [Tooltip("HolyBulletPickup.prefab (wrapping PistolBulletAmmo.prefab).")]
    [SerializeField] private HolyBulletPickup bulletPickupPrefab;

    [Tooltip("Maximum bullets active in the world simultaneously (always maintains 3; picking one drops 3->2 then respawns 2->3 in a different area!).")]
    [Range(1, 10)]
    [SerializeField] private int maxActiveWorldBullets = 3;

    [Tooltip("Short delay (seconds) after collecting a bullet before its replacement spawns in a different area.")]
    [Range(0.0f, 5.0f)]
    [SerializeField] private float respawnDelaySeconds = 0.35f;

    [Header("Non-Repeating & Anti-Overlap Rules")]
    [Tooltip("Minimum horizontal distance (meters) between any two active world bullets.")]
    [Range(0.5f, 15.0f)]
    [SerializeField] private float minDistanceFromActiveBullets = 3.5f;

    [Tooltip("Preferred minimum distance (meters) from recent spawn spots so bullets never repeat the same place.")]
    [Range(0.15f, 8.0f)]
    [SerializeField] private float minDistanceFromRecentSpawns = 1.25f;

    [Tooltip("Hard non-identical position floor (meters). Even inside small table zones, bullets never spawn at the exact same spot.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float hardNonIdenticalMinDistance = 0.12f;

    [Tooltip("Minimum distance (meters) from the player's VR head when spawning a replacement bullet.")]
    [Range(0.5f, 10.0f)]
    [SerializeField] private float minDistanceFromPlayer = 3.0f;

    [Tooltip("Sphere clearance radius (meters) to guarantee bullets never spawn inside walls or furniture.")]
    [Range(0.08f, 0.6f)]
    [SerializeField] private float wallClearanceRadius = 0.22f;

    [Tooltip("Vertical lift (meters) above the hit floor/tabletop surface.")]
    [Range(0.02f, 0.25f)]
    [SerializeField] private float surfaceHoverHeight = 0.06f;

    [Tooltip("Layers considered solid floors/tables/walls for surface snapping and obstacle avoidance.")]
    [SerializeField] private LayerMask environmentLayers = ~0;

    [Header("Spawn Zones & References")]
    [Tooltip("Place 5-8 HolyBulletSpawnZone empties around rooms/tables. Automatically finds all zones in the scene if left empty.")]
    [SerializeField] private List<HolyBulletSpawnZone> spawnZones = new List<HolyBulletSpawnZone>();

    [Tooltip("Player VR Camera / Head. Automatically finds Camera.main if left empty.")]
    [SerializeField] private Transform playerHead;

    [Tooltip("If true, listens to RoundManager.onAtticUnlocked so locked Attic zones open automatically after Capture #3.")]
    [SerializeField] private bool autoUnlockLockedZonesOnAtticUnlock = true;

    [Header("Current State (Read Only)")]
    [SerializeField] private int activeWorldBulletCount = 0;
    [SerializeField] private int totalBulletsCollected = 0;

    [Header("Events")]
    [Tooltip("Invoked whenever a world bullet spawns. Passes world position.")]
    public UnityEvent<Vector3> onWorldBulletSpawned;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private const int k_RecentHistoryCapacity = 12;
    private readonly List<HolyBulletPickup> m_ActiveBullets = new List<HolyBulletPickup>();
    private readonly List<HolyBulletSpawnZone> m_CandidateZones = new List<HolyBulletSpawnZone>();
    private readonly Queue<Vector3> m_RecentSpawnPositions = new Queue<Vector3>(k_RecentHistoryCapacity);
    private readonly Collider[] m_OverlapBuffer = new Collider[12];
    private readonly RaycastHit[] m_RayBuffer = new RaycastHit[12];
    private int m_PendingRespawnCoroutines = 0;
    private Transform m_RuntimeBulletsContainer;

    public int ActiveWorldBulletCount => m_ActiveBullets.Count;
    public int MaxActiveWorldBullets => maxActiveWorldBullets;
    public int TotalBulletsCollected => totalBulletsCollected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HolyBulletSpawner] Duplicate HolyBulletSpawner detected in scene.", this);
        }
        Instance = this;

        GameObject containerObj = new GameObject("ActiveHolyBullets_Runtime");
        containerObj.transform.SetParent(transform, false);
        m_RuntimeBulletsContainer = containerObj.transform;
    }

    private void Start()
    {
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        if (spawnZones.Count == 0)
        {
            HolyBulletSpawnZone[] found = FindObjectsByType<HolyBulletSpawnZone>(FindObjectsSortMode.None);
            spawnZones.AddRange(found);
        }

        if (autoUnlockLockedZonesOnAtticUnlock && RoundManager.Instance != null)
        {
            RoundManager.Instance.onAtticUnlocked.AddListener(UnlockAllLockedZones);
        }

        SpawnUpToCapacity(excludedZone: null);
    }

    private void OnDestroy()
    {
        if (autoUnlockLockedZonesOnAtticUnlock && RoundManager.Instance != null)
        {
            RoundManager.Instance.onAtticUnlocked.RemoveListener(UnlockAllLockedZones);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Called by HolyBulletPickup when the player collects a bullet (3 -> 2 -> schedules replacement 2 -> 3 in a different area!).
    /// </summary>
    public void NotifyBulletCollected(HolyBulletPickup collectedBullet, HolyBulletSpawnZone sourceZone, Vector3 collectedPosition)
    {
        m_ActiveBullets.Remove(collectedBullet);
        activeWorldBulletCount = m_ActiveBullets.Count;
        totalBulletsCollected++;

        RecordRecentSpawnPosition(collectedPosition);

        if (showDebugLogs)
        {
            string zoneLabel = sourceZone != null ? sourceZone.AreaName : "Unknown";
            Debug.Log($"[HolyBulletSpawner] Bullet collected from '{zoneLabel}' (Active world bullets: {activeWorldBulletCount}/{maxActiveWorldBullets} | Total found: {totalBulletsCollected}). Spawning replacement in a different area...");
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(RespawnReplacementAfterDelay(sourceZone));
        }
    }

    private IEnumerator RespawnReplacementAfterDelay(HolyBulletSpawnZone excludedZone)
    {
        m_PendingRespawnCoroutines++;
        if (respawnDelaySeconds > 0.01f)
        {
            yield return new WaitForSeconds(respawnDelaySeconds);
        }
        m_PendingRespawnCoroutines = Mathf.Max(0, m_PendingRespawnCoroutines - 1);

        PruneDestroyedBullets();
        if (m_ActiveBullets.Count < maxActiveWorldBullets)
        {
            TrySpawnSingleBullet(excludedZone);
        }
    }

    public void UnlockAllLockedZones()
    {
        int unlockedCount = 0;
        for (int i = 0; i < spawnZones.Count; i++)
        {
            if (spawnZones[i] != null && !spawnZones[i].IsUnlocked)
            {
                spawnZones[i].UnlockZone();
                unlockedCount++;
            }
        }

        if (showDebugLogs && unlockedCount > 0)
        {
            Debug.Log($"[HolyBulletSpawner] Unlocked {unlockedCount} additional Holy Bullet spawn zone(s) (Attic unlocked)!");
        }
    }

    [ContextMenu("Respawn All World Bullets (Test)")]
    public void RespawnAllWorldBullets()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[HolyBulletSpawner] Enter Play Mode to test respawning bullets.");
            return;
        }

        StopAllCoroutines();
        m_PendingRespawnCoroutines = 0;

        for (int i = m_ActiveBullets.Count - 1; i >= 0; i--)
        {
            if (m_ActiveBullets[i] != null)
            {
                Destroy(m_ActiveBullets[i].gameObject);
            }
        }
        m_ActiveBullets.Clear();
        activeWorldBulletCount = 0;

        SpawnUpToCapacity(excludedZone: null);
    }

    [ContextMenu("Simulate Collect First Bullet (Test)")]
    public void SimulateCollectFirstBullet()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[HolyBulletSpawner] Enter Play Mode to simulate collecting a bullet.");
            return;
        }

        PruneDestroyedBullets();
        if (m_ActiveBullets.Count > 0 && m_ActiveBullets[0] != null)
        {
            m_ActiveBullets[0].TryCollectForPistol();
        }
    }

    private void SpawnUpToCapacity(HolyBulletSpawnZone excludedZone)
    {
        PruneDestroyedBullets();
        int safetyLimit = maxActiveWorldBullets * 2;
        int attempts = 0;

        while (m_ActiveBullets.Count + m_PendingRespawnCoroutines < maxActiveWorldBullets && attempts < safetyLimit)
        {
            attempts++;
            if (!TrySpawnSingleBullet(excludedZone))
                break;
        }
    }

    public bool TrySpawnSingleBullet(HolyBulletSpawnZone excludedZone)
    {
        if (bulletPickupPrefab == null)
        {
            Debug.LogError("[HolyBulletSpawner] No bulletPickupPrefab assigned!", this);
            return false;
        }

        if (!TrySelectSpawnLocation(excludedZone, out HolyBulletSpawnZone chosenZone, out Vector3 spawnPos))
        {
            Debug.LogWarning("[HolyBulletSpawner] Could not find a clear spawn point across available HolyBulletSpawnZones.", this);
            return false;
        }

        HolyBulletPickup spawned = Instantiate(bulletPickupPrefab, spawnPos, Quaternion.identity, m_RuntimeBulletsContainer);
        spawned.name = $"HolyBulletPickup_{(chosenZone != null ? chosenZone.AreaName : "Zone")}";
        spawned.InitializeFromSpawner(this, chosenZone);

        m_ActiveBullets.Add(spawned);
        activeWorldBulletCount = m_ActiveBullets.Count;
        RecordRecentSpawnPosition(spawnPos);

        if (showDebugLogs)
        {
            string zoneLabel = chosenZone != null ? chosenZone.AreaName : "Fallback";
            Debug.Log($"[HolyBulletSpawner] Spawned Holy Bullet in '{zoneLabel}' at {spawnPos:F2} (Active: {activeWorldBulletCount}/{maxActiveWorldBullets})");
        }

        onWorldBulletSpawned?.Invoke(spawnPos);
        return true;
    }

    /// <summary>
    /// Selects a valid world position preferring a DIFFERENT unlocked zone from excludedZone,
    /// avoiding walls, other active bullets, the player, and recent exact coordinates.
    /// </summary>
    public bool TrySelectSpawnLocation(
        HolyBulletSpawnZone excludedZone,
        out HolyBulletSpawnZone chosenZone,
        out Vector3 validSpawnPos)
    {
        chosenZone = null;
        validSpawnPos = Vector3.zero;

        BuildShuffledCandidateZones(excludedZone);
        if (m_CandidateZones.Count == 0)
            return false;

        // Pass 1: Strict (different zone + full active separation + full recent separation + player distance)
        if (SearchZonesForCandidate(
            preferUnoccupiedZones: true,
            activeMinDist: minDistanceFromActiveBullets,
            recentMinDist: minDistanceFromRecentSpawns,
            playerMinDist: minDistanceFromPlayer,
            out chosenZone,
            out validSpawnPos))
        {
            return true;
        }

        // Pass 2: Relaxed active/player separation (still different zone preference + >= hardNonIdenticalMinDistance!)
        if (SearchZonesForCandidate(
            preferUnoccupiedZones: false,
            activeMinDist: Mathf.Min(1.2f, minDistanceFromActiveBullets * 0.4f),
            recentMinDist: hardNonIdenticalMinDistance,
            playerMinDist: Mathf.Min(1.5f, minDistanceFromPlayer * 0.5f),
            out chosenZone,
            out validSpawnPos))
        {
            return true;
        }

        // Pass 3: Guaranteed fallback across any unlocked zone (still guarantees non-identical coordinates!)
        for (int i = 0; i < spawnZones.Count; i++)
        {
            HolyBulletSpawnZone z = spawnZones[i];
            if (z == null || !z.IsUnlocked)
                continue;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector3 raw = z.SampleRandomWorldPoint();
                if (TrySnapToClearSurface(z, raw, out Vector3 snapped) && IsFarEnoughFromRecent(snapped, hardNonIdenticalMinDistance))
                {
                    chosenZone = z;
                    validSpawnPos = snapped;
                    return true;
                }
            }
        }

        return false;
    }

    private void BuildShuffledCandidateZones(HolyBulletSpawnZone excludedZone)
    {
        m_CandidateZones.Clear();
        for (int i = 0; i < spawnZones.Count; i++)
        {
            HolyBulletSpawnZone zone = spawnZones[i];
            if (zone == null || !zone.gameObject.activeInHierarchy || !zone.IsUnlocked)
                continue;

            if (zone == excludedZone)
                continue;

            m_CandidateZones.Add(zone);
        }

        // Fisher-Yates shuffle so area selection is genuinely varied
        for (int i = m_CandidateZones.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            HolyBulletSpawnZone tmp = m_CandidateZones[i];
            m_CandidateZones[i] = m_CandidateZones[j];
            m_CandidateZones[j] = tmp;
        }

        // Only append excludedZone at the very back as a last resort if there's only 1 zone in the scene
        if (excludedZone != null && excludedZone.gameObject.activeInHierarchy && excludedZone.IsUnlocked)
        {
            m_CandidateZones.Add(excludedZone);
        }
    }

    private bool SearchZonesForCandidate(
        bool preferUnoccupiedZones,
        float activeMinDist,
        float recentMinDist,
        float playerMinDist,
        out HolyBulletSpawnZone chosenZone,
        out Vector3 validPos)
    {
        chosenZone = null;
        validPos = Vector3.zero;

        for (int pass = 0; pass < (preferUnoccupiedZones ? 2 : 1); pass++)
        {
            bool skipOccupied = preferUnoccupiedZones && pass == 0;

            for (int zIdx = 0; zIdx < m_CandidateZones.Count; zIdx++)
            {
                HolyBulletSpawnZone zone = m_CandidateZones[zIdx];
                if (skipOccupied && ZoneAlreadyHasActiveBullet(zone))
                    continue;

                const int samplesPerZone = 18;
                for (int s = 0; s < samplesPerZone; s++)
                {
                    Vector3 rawCandidate = zone.SampleRandomWorldPoint();
                    if (!TrySnapToClearSurface(zone, rawCandidate, out Vector3 snapped))
                        continue;

                    if (!IsFarEnoughFromActiveBullets(snapped, activeMinDist))
                        continue;

                    if (!IsFarEnoughFromRecent(snapped, recentMinDist))
                        continue;

                    if (!IsFarEnoughFromPlayer(snapped, playerMinDist))
                        continue;

                    chosenZone = zone;
                    validPos = snapped;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TrySnapToClearSurface(HolyBulletSpawnZone zone, Vector3 rawPoint, out Vector3 snappedPos)
    {
        snappedPos = rawPoint;
        Vector3 rayOrigin = new Vector3(rawPoint.x, zone.transform.position.y + zone.RaycastStartHeight, rawPoint.z);
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, m_RayBuffer, zone.RaycastMaxDistance, environmentLayers, QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        bool foundSurface = false;
        Vector3 surfacePoint = rawPoint;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = m_RayBuffer[i];
            if (hit.collider == null || hit.normal.y < 0.65f)
                continue;

            if (hit.collider.GetComponentInParent<GhostTarget>() != null ||
                hit.collider.GetComponentInParent<HolyPistolController>() != null ||
                hit.collider.GetComponentInParent<HolyBulletPickup>() != null)
                continue;

            if (playerHead != null && hit.collider.transform.IsChildOf(playerHead.root))
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                surfacePoint = hit.point;
                foundSurface = true;
            }
        }

        if (!foundSurface)
            return false;

        Vector3 elevated = surfacePoint + Vector3.up * surfaceHoverHeight;
        // Check horizontal wall clearance slightly above the floor so the floor itself isn't counted as a wall
        Vector3 wallCheckCenter = surfacePoint + Vector3.up * Mathf.Max(0.28f, wallClearanceRadius + 0.08f);
        int overlapCount = Physics.OverlapSphereNonAlloc(wallCheckCenter, wallClearanceRadius, m_OverlapBuffer, environmentLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = m_OverlapBuffer[i];
            if (col == null)
                continue;

            if (col.bounds.max.y <= surfacePoint.y + 0.08f)
                continue;

            if (col.GetComponentInParent<GhostTarget>() != null ||
                col.GetComponentInParent<HolyBulletPickup>() != null)
                continue;

            return false;
        }

        snappedPos = elevated;
        return true;
    }

    private bool ZoneAlreadyHasActiveBullet(HolyBulletSpawnZone zone)
    {
        for (int i = 0; i < m_ActiveBullets.Count; i++)
        {
            if (m_ActiveBullets[i] != null && m_ActiveBullets[i].SourceZone == zone)
                return true;
        }
        return false;
    }

    private bool IsFarEnoughFromActiveBullets(Vector3 pos, float minDist)
    {
        float minSq = minDist * minDist;
        for (int i = 0; i < m_ActiveBullets.Count; i++)
        {
            if (m_ActiveBullets[i] == null)
                continue;

            Vector3 other = m_ActiveBullets[i].transform.position;
            float dx = pos.x - other.x;
            float dz = pos.z - other.z;
            if ((dx * dx + dz * dz) < minSq)
                return false;
        }
        return true;
    }

    private bool IsFarEnoughFromRecent(Vector3 pos, float minDist)
    {
        float minSq = minDist * minDist;
        foreach (Vector3 recent in m_RecentSpawnPositions)
        {
            float dx = pos.x - recent.x;
            float dz = pos.z - recent.z;
            if ((dx * dx + dz * dz) < minSq)
                return false;
        }
        return true;
    }

    private bool IsFarEnoughFromPlayer(Vector3 pos, float minDist)
    {
        if (playerHead == null)
            return true;

        float dx = pos.x - playerHead.position.x;
        float dz = pos.z - playerHead.position.z;
        return (dx * dx + dz * dz) >= (minDist * minDist);
    }

    private void RecordRecentSpawnPosition(Vector3 pos)
    {
        if (m_RecentSpawnPositions.Count >= k_RecentHistoryCapacity)
        {
            m_RecentSpawnPositions.Dequeue();
        }
        m_RecentSpawnPositions.Enqueue(pos);
    }

    private void PruneDestroyedBullets()
    {
        for (int i = m_ActiveBullets.Count - 1; i >= 0; i--)
        {
            if (m_ActiveBullets[i] == null)
            {
                m_ActiveBullets.RemoveAt(i);
            }
        }
        activeWorldBulletCount = m_ActiveBullets.Count;
    }
}
