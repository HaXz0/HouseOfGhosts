using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Height calibration shared by the wrist menu and the door main menu.
/// The player stands up straight and looks ahead; the headset's eye height above the floor is
/// averaged and converted to body height, which then drives arm length and shoulder position.
/// Start <see cref="Run"/> with StartCoroutine; stop the coroutine to cancel.
/// </summary>
public static class ArmCalibration
{
    public const float NoProgress = -1f;

    const float k_CountdownTime = 3f;
    const float k_SampleTime = 1f;
    const float k_Timeout = 10f;
    const float k_MaximumHeadPitch = 15f;
    const float k_MaximumHeightDrift = 0.03f;
    const float k_RangeTolerance = 0.1f;

    public static string FormatHeight(float heightInMeters)
    {
        return $"{Mathf.RoundToInt(heightInMeters * 100f)} cm";
    }

    /// <param name="arms">The arms to calibrate.</param>
    /// <param name="setStatus">Receives a progress message and a 0-1 progress value (<see cref="NoProgress"/> hides the bar).</param>
    /// <param name="onApplied">Called after the new height was applied successfully.</param>
    /// <param name="onFinished">Called once at the end with the final message (success or failure).</param>
    public static IEnumerator Run(ArmsVRController arms, Action<string, float> setStatus, Action onApplied, Action<string> onFinished)
    {
        var elapsed = 0f;
        while (elapsed < k_CountdownTime)
        {
            setStatus?.Invoke($"HEIGHT CALIBRATION\nStand up straight and look ahead\n{Mathf.CeilToInt(k_CountdownTime - elapsed)}", NoProgress);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        var totalTime = 0f;
        var sampleTime = 0f;
        var sampleCount = 0;
        var eyeHeightTotal = 0f;
        var referenceEyeHeight = 0f;
        while (sampleTime < k_SampleTime)
        {
            if (arms == null || !arms.isActiveAndEnabled)
            {
                onFinished?.Invoke("ARMS UNAVAILABLE");
                yield break;
            }
            if (totalTime > k_Timeout)
            {
                onFinished?.Invoke("CALIBRATION FAILED\nStand still and look straight ahead");
                yield break;
            }
            if (!arms.TryGetEyeHeightSample(out var eyeHeight, out var headPitch, out var error))
            {
                onFinished?.Invoke("CALIBRATION FAILED\n" + error);
                yield break;
            }

            string hint = null;
            if (Mathf.Abs(headPitch) > k_MaximumHeadPitch)
                hint = headPitch < 0f ? "Look up - straight ahead" : "Look down - straight ahead";
            else if (sampleCount > 0 && Mathf.Abs(eyeHeight - referenceEyeHeight) > k_MaximumHeightDrift)
                hint = "Hold still";

            if (hint != null)
            {
                // Restart sampling instead of failing, so a small slip does not force a new countdown.
                sampleTime = 0f;
                sampleCount = 0;
                eyeHeightTotal = 0f;
                setStatus?.Invoke("HEIGHT CALIBRATION\n" + hint, 0f);
            }
            else
            {
                if (sampleCount == 0)
                    referenceEyeHeight = eyeHeight;
                eyeHeightTotal += eyeHeight;
                sampleCount++;
                sampleTime += Time.unscaledDeltaTime;
                setStatus?.Invoke("HEIGHT CALIBRATION\nMeasuring... hold still", Mathf.Clamp01(sampleTime / k_SampleTime));
            }

            totalTime += Time.unscaledDeltaTime;
            yield return null;
        }

        if (sampleCount == 0)
        {
            onFinished?.Invoke("CALIBRATION FAILED\nNo tracking samples");
            yield break;
        }
        if (arms == null || !arms.isActiveAndEnabled)
        {
            onFinished?.Invoke("ARMS UNAVAILABLE");
            yield break;
        }

        var height = ArmsVRController.EyeHeightToBodyHeight(eyeHeightTotal / sampleCount);
        if (height < ArmsVRController.MinimumHeight - k_RangeTolerance || height > ArmsVRController.MaximumHeight + k_RangeTolerance)
        {
            onFinished?.Invoke($"CALIBRATION FAILED\nMeasured {FormatHeight(height)} - stand up straight\nor set your height with the slider");
            yield break;
        }

        arms.SetPlayerHeight(height);
        onApplied?.Invoke();
        onFinished?.Invoke($"CALIBRATION COMPLETE\nHEIGHT: {FormatHeight(arms.PlayerHeight)}");
    }
}
