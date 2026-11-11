using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GhostTarget))]
[DisallowMultipleComponent]
public class GhostAI : MonoBehaviour
{
    public enum GhostState
    {
        Hidden,
        WatchingPlayer, // Behaviour A: Brief pause and stare
        Fleeing,        // Behaviour B or after staring
        Captured
    }

    [Header("Current State (Read Only)")]
    [SerializeField] private GhostState currentState = GhostState.Hidden;

    [Header("Fake Escape Settings")]
    [Tooltip("Probability (0.5 = 50%) for Behaviour A (stare at player before fleeing).")]
    [Range(0f, 1f)]
    [SerializeField] private float watchPlayerChance = 0.5f;

    [Tooltip("How many seconds the ghost freezes and stares directly at you during Behaviour A.")]
    [SerializeField] private float watchPlayerDuration = 2.0f;

    [Header("Movement Settings")]
    [Tooltip("Ghost movement speed when fleeing towards the next hiding spot (meters/second).")]
    [SerializeField] private float fleeSpeed = 2.8f;

    [Tooltip("Speed of turning when rotating to face you or the next spot.")]
    [SerializeField] private float rotationSpeed = 6.0f;

    [Header("Trigger Conditions")]
    [Tooltip("Distance in meters that triggers ghost escape if you walk too close to her hiding spot.")]
    [SerializeField] private float triggerProximityDistance = 2.5f;

    [Tooltip("If true, shining the flashlight beam on her while hidden triggers her Fake Escape.")]
    [SerializeField] private bool triggerOnFlashlightHit = true;

    [Header("References")]
    [Tooltip("Player VR Headset / Camera. If left empty, automatically finds Camera.main.")]
    [SerializeField] private Transform playerHead;

    [Tooltip("List of hiding spots. If empty, automatically finds all GhostHidingSpot objects in the scene.")]
    [SerializeField] private List<GhostHidingSpot> hidingSpots = new List<GhostHidingSpot>();

    private GhostTarget m_GhostTarget;
    private GhostHidingSpot m_CurrentSpot;
    private GhostHidingSpot m_TargetSpot;
    private float m_WatchTimer = 0f;

    public GhostState CurrentState => currentState;

    private void Awake()
    {
        m_GhostTarget = GetComponent<GhostTarget>();
    }

    private void Start()
    {
        // Automatically find the player's head camera if unassigned
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        // Automatically find all hiding spots in the scene if list is empty
        if (hidingSpots.Count == 0)
        {
            hidingSpots.AddRange(FindObjectsByType<GhostHidingSpot>(FindObjectsSortMode.None));
        }

        // Listen for capture to stop AI
        if (m_GhostTarget != null)
        {
            m_GhostTarget.onGhostCaptured.AddListener(OnCaptured);
        }

        // Place ghost at first hiding spot at start
        if (hidingSpots.Count > 0)
        {
            m_CurrentSpot = hidingSpots[0];
            transform.position = m_CurrentSpot.Position;
        }
    }

    private void OnDestroy()
    {
        if (m_GhostTarget != null)
        {
            m_GhostTarget.onGhostCaptured.RemoveListener(OnCaptured);
        }
    }

    private void Update()
    {
        if (currentState == GhostState.Captured)
            return;

        switch (currentState)
        {
            case GhostState.Hidden:
                UpdateHidden();
                break;

            case GhostState.WatchingPlayer:
                UpdateWatchingPlayer();
                break;

            case GhostState.Fleeing:
                UpdateFleeing();
                break;
        }
    }

    private void UpdateHidden()
    {
        // Trigger Check 1: Player walked too close
        if (playerHead != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerHead.position);
            if (distanceToPlayer <= triggerProximityDistance)
            {
                TriggerFakeEscape("Player got too close!");
                return;
            }
        }

        // Trigger Check 2: Flashlight beam illuminated the ghost
        if (triggerOnFlashlightHit && m_GhostTarget != null && m_GhostTarget.IsIlluminated)
        {
            TriggerFakeEscape("Flashlight beam spotted the ghost!");
        }
    }

    private void UpdateWatchingPlayer()
    {
        // Behaviour A: Rotate smoothly to face player headset
        if (playerHead != null)
        {
            Vector3 directionToPlayer = playerHead.position - transform.position;
            directionToPlayer.y = 0f; // Keep rotation upright

            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        m_WatchTimer -= Time.deltaTime;
        if (m_WatchTimer <= 0f)
        {
            Debug.Log("[GhostAI] Behaviour A stare finished! Now fleeing...");
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
        Vector3 moveDir = targetPos - transform.position;
        float distance = moveDir.magnitude;

        // Rotate towards next hiding spot
        if (distance > 0.05f)
        {
            Vector3 flatDir = moveDir;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // Move towards target spot (can phase straight through walls)
        transform.position = Vector3.MoveTowards(transform.position, targetPos, fleeSpeed * Time.deltaTime);

        // Arrived at hiding spot
        if (distance <= 0.15f)
        {
            m_CurrentSpot = m_TargetSpot;
            currentState = GhostState.Hidden;
            Debug.Log($"[GhostAI] Ghost safely reached hiding spot: {m_CurrentSpot.SpotName}. Hidden again.");
        }
    }

    /// <summary>
    /// Triggers the Fake Escape sequence (randomly chooses Behaviour A or B).
    /// </summary>
    [ContextMenu("Trigger Fake Escape")]
    public void TriggerFakeEscape(string reason = "Triggered")
    {
        if (currentState != GhostState.Hidden)
            return;

        bool chooseWatch = Random.value <= watchPlayerChance;

        if (chooseWatch)
        {
            // Behaviour A: Stare at player
            currentState = GhostState.WatchingPlayer;
            m_WatchTimer = watchPlayerDuration;
            Debug.Log($"[GhostAI] {reason} -> Fake Escape [Behaviour A: Staring at player for {watchPlayerDuration:F1}s]");
        }
        else
        {
            // Behaviour B: Bolt immediately
            Debug.Log($"[GhostAI] {reason} -> Fake Escape [Behaviour B: Immediate Escape!]");
            StartFleeing();
        }
    }

    private void StartFleeing()
    {
        m_TargetSpot = GetNextHidingSpot();
        currentState = GhostState.Fleeing;

        if (m_TargetSpot != null)
        {
            Debug.Log($"[GhostAI] Ghost fleeing towards: {m_TargetSpot.SpotName}");
        }
    }

    private GhostHidingSpot GetNextHidingSpot()
    {
        if (hidingSpots.Count <= 1)
            return m_CurrentSpot;

        List<GhostHidingSpot> validSpots = new List<GhostHidingSpot>(hidingSpots);
        if (m_CurrentSpot != null)
            validSpots.Remove(m_CurrentSpot);

        int randomIndex = Random.Range(0, validSpots.Count);
        return validSpots[randomIndex];
    }

    private void OnCaptured()
    {
        currentState = GhostState.Captured;
        Debug.Log("[GhostAI] Ghost captured! AI stopped.");
    }
}