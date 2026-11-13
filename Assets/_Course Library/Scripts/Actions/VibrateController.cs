using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Vibrate the XR Controller
/// </summary>
public class VibrateController : MonoBehaviour
{
    public float strongVibrate = 0.75f;
    public float weakVibrate = 0.25f;

    private IXRHapticImpulseProvider hapticImpulseProvider = null;

    private void Awake()
    {
        hapticImpulseProvider = GetComponent<IXRHapticImpulseProvider>();
    }

    public void Vibrate(float amplitude, float duration)
    {
        SendImpulse(amplitude, duration);
    }

    public void VibrateWeak(float duration)
    {
        SendImpulse(weakVibrate, duration);
    }

    public void VibrateStrong(float duration)
    {
        SendImpulse(strongVibrate, duration);
    }

    private void SendImpulse(float amplitude, float duration)
    {
        hapticImpulseProvider?.GetChannelGroup()?.GetChannel()?.SendHapticImpulse(amplitude, duration);
    }
}
