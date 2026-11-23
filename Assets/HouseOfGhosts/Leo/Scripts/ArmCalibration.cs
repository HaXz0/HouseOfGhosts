using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// T-pose arm calibration shared by the wrist menu and the door main menu.
/// Start <see cref="Run"/> with StartCoroutine; stop the coroutine to cancel.
/// </summary>
public static class ArmCalibration
{
    const float k_PreparationTime = 5f;
    const float k_SampleTime = 1.25f;
    const float k_MaximumDrift = 0.06f;

    /// <param name="arms">The arms to calibrate.</param>
    /// <param name="setStatus">Receives progress messages to show to the player.</param>
    /// <param name="onApplied">Called after new arm scales were applied successfully.</param>
    /// <param name="onFinished">Called once at the end with the final message (success or failure).</param>
    public static IEnumerator Run(ArmsVRController arms, Action<string> setStatus, Action onApplied, Action<string> onFinished)
    {
        var elapsed = 0f;
        while (elapsed < k_PreparationTime)
        {
            setStatus?.Invoke($"ARM CALIBRATION\nStretch both arms sideways\nHold still in {Mathf.CeilToInt(k_PreparationTime - elapsed)}");
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        var sampleCount = 0;
        var leftScaleTotal = 0f;
        var rightScaleTotal = 0f;
        var firstLeft = Vector3.zero;
        var firstRight = Vector3.zero;
        while (elapsed < k_SampleTime)
        {
            if (arms == null)
            {
                onFinished?.Invoke("ARMS UNAVAILABLE");
                yield break;
            }
            if (!arms.TryGetCalibrationSample(out var leftScale, out var rightScale, out var leftWrist, out var rightWrist, out var error))
            {
                onFinished?.Invoke("CALIBRATION FAILED\n" + error);
                yield break;
            }

            if (sampleCount == 0)
            {
                firstLeft = leftWrist;
                firstRight = rightWrist;
            }
            else if (Vector3.Distance(firstLeft, leftWrist) > k_MaximumDrift || Vector3.Distance(firstRight, rightWrist) > k_MaximumDrift)
            {
                onFinished?.Invoke("CALIBRATION FAILED\nHold both arms still");
                yield break;
            }

            setStatus?.Invoke("ARM CALIBRATION\nHold still...");
            leftScaleTotal += leftScale;
            rightScaleTotal += rightScale;
            sampleCount++;
            elapsed += Time.unscaledDeltaTime;
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

        arms.SetArmScales(leftScaleTotal / sampleCount, rightScaleTotal / sampleCount);
        onApplied?.Invoke();
        onFinished?.Invoke("CALIBRATION COMPLETE\nEstimated reach - adjust to fit");
    }
}
