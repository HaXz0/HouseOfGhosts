using UnityEngine;

/// <summary>
/// Movement and comfort preferences shared by the door main menu and the wrist menu.
/// The PlayerPrefs keys keep their original "PlayerTestScene." prefix so existing saves still load.
/// </summary>
public sealed class ComfortSettings
{
    public const float MinMoveSpeed = 0.5f;
    public const float MaxMoveSpeed = 3.5f;
    public const float MinSnapAngle = 15f;
    public const float MaxSnapAngle = 90f;
    public const float MinTurnSpeed = 20f;
    public const float MaxTurnSpeed = 120f;
    public const float MinVignetteStrength = 0f;
    public const float MaxVignetteStrength = 0.8f;

    const string k_MovementKey = "PlayerTestScene.Movement";
    const string k_TurnKey = "PlayerTestScene.Turn";
    const string k_MoveSpeedKey = "PlayerTestScene.MoveSpeed";
    const string k_SnapAngleKey = "PlayerTestScene.SnapAngle";
    const string k_TurnSpeedKey = "PlayerTestScene.TurnSpeed";
    const string k_VignetteKey = "PlayerTestScene.Vignette";
    const string k_VignetteStrengthKey = "PlayerTestScene.VignetteStrength";

    public bool SmoothMovement { get; set; }
    public bool SmoothTurning { get; set; }
    public float MoveSpeed { get; set; }
    public float SnapAngle { get; set; }
    public float TurnSpeed { get; set; }
    public bool VignetteEnabled { get; set; }
    public float VignetteStrength { get; set; }

    /// <summary>
    /// Loads saved settings. The defaults are used the first time, before anything was saved.
    /// </summary>
    public static ComfortSettings Load(float defaultMoveSpeed, float defaultSnapAngle, float defaultTurnSpeed)
    {
        return new ComfortSettings
        {
            SmoothMovement = PlayerPrefs.GetInt(k_MovementKey, 0) == 1,
            SmoothTurning = PlayerPrefs.GetInt(k_TurnKey, 0) == 1,
            MoveSpeed = PlayerPrefs.GetFloat(k_MoveSpeedKey, defaultMoveSpeed),
            SnapAngle = PlayerPrefs.GetFloat(k_SnapAngleKey, defaultSnapAngle),
            TurnSpeed = PlayerPrefs.GetFloat(k_TurnSpeedKey, defaultTurnSpeed),
            VignetteEnabled = PlayerPrefs.GetInt(k_VignetteKey, 1) == 1,
            VignetteStrength = PlayerPrefs.GetFloat(k_VignetteStrengthKey, 0.55f),
        };
    }

    public void Save()
    {
        PlayerPrefs.SetInt(k_MovementKey, SmoothMovement ? 1 : 0);
        PlayerPrefs.SetInt(k_TurnKey, SmoothTurning ? 1 : 0);
        PlayerPrefs.SetFloat(k_MoveSpeedKey, MoveSpeed);
        PlayerPrefs.SetFloat(k_SnapAngleKey, SnapAngle);
        PlayerPrefs.SetFloat(k_TurnSpeedKey, TurnSpeed);
        PlayerPrefs.SetInt(k_VignetteKey, VignetteEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(k_VignetteStrengthKey, VignetteStrength);
        PlayerPrefs.Save();
    }
}
