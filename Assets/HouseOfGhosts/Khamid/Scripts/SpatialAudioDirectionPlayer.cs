using UnityEngine;

public enum SpatialDirection
{
    Front,
    Back,
    Left,
    Right
}

/// <summary>
/// Plays a 3D sound from a direction relative to the player's head.
/// The sound is placed in the world once and does not follow the head,
/// so turning your head while it plays changes where you hear it.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class SpatialAudioDirectionPlayer : MonoBehaviour
{
    [Tooltip("The player's head. Use the Main Camera inside the XR Origin.")]
    [SerializeField] private Transform playerHead;

    [Tooltip("Sound to play. If empty, the AudioClip on the AudioSource is used.")]
    [SerializeField] private AudioClip clip;

    [Tooltip("How far from the head the sound is placed, in meters.")]
    [SerializeField, Min(0.5f)] private float distance = 2f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        // Uses the HRTF spatializer from Project Settings > Audio (Meta XR Audio).
        // Meta ships no Linux plugin, so the Linux Editor falls back to Unity's normal 3D panning.
#if !UNITY_EDITOR_LINUX
        audioSource.spatialize = true;
#endif

        if (clip == null)
            clip = audioSource.clip;

        if (playerHead == null && Camera.main != null)
            playerHead = Camera.main.transform;
    }

    public void PlayFromDirection(SpatialDirection direction)
    {
        if (playerHead == null || clip == null)
        {
            Debug.LogWarning("SpatialAudioDirectionPlayer needs a Player Head and an Audio Clip.", this);
            return;
        }

        transform.position = playerHead.position + GetWorldDirection(direction) * distance;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

    private Vector3 GetWorldDirection(SpatialDirection direction)
    {
        // Only use the head's left/right turn so looking up or down does not move the sound vertically.
        var forward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(playerHead.up, Vector3.up);
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);

        switch (direction)
        {
            case SpatialDirection.Back: return -forward;
            case SpatialDirection.Left: return -right;
            case SpatialDirection.Right: return right;
            default: return forward;
        }
    }
}
