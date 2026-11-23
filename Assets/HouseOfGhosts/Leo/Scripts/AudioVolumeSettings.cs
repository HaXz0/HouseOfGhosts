using UnityEngine;

/// <summary>
/// Saved master volume. It is applied automatically when the game starts, so every scene uses it.
/// </summary>
public static class AudioVolumeSettings
{
    const string k_MasterVolumeKey = "Settings.MasterVolume";

    public static float MasterVolume => PlayerPrefs.GetFloat(k_MasterVolumeKey, 1f);

    public static void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(k_MasterVolumeKey, volume);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedVolume()
    {
        AudioListener.volume = MasterVolume;
    }
}
