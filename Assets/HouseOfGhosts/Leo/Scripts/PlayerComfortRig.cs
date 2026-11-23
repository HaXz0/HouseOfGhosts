using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Applies <see cref="ComfortSettings"/> to the XR Origin's locomotion providers and tunneling vignette.
/// Create it once (e.g. in Start) with <see cref="FindInScene"/>; it does not search the scene again afterwards.
/// </summary>
public sealed class PlayerComfortRig
{
    readonly Dictionary<LocomotionProvider, bool> m_ProviderStates = new Dictionary<LocomotionProvider, bool>();
    readonly List<LocomotionVignetteProvider> m_VignetteProviders = new List<LocomotionVignetteProvider>();

    ControllerInputActionManager[] m_InputManagers;
    DynamicMoveProvider[] m_MoveProviders;
    SnapTurnProvider[] m_SnapTurnProviders;
    ContinuousTurnProvider[] m_ContinuousTurnProviders;
    LocomotionProvider[] m_LocomotionProviders;
    TunnelingVignetteController m_VignetteController;

    public float DefaultMoveSpeed => m_MoveProviders.Length > 0 ? m_MoveProviders[0].moveSpeed : 2.5f;
    public float DefaultSnapAngle => m_SnapTurnProviders.Length > 0 ? m_SnapTurnProviders[0].turnAmount : 45f;
    public float DefaultTurnSpeed => m_ContinuousTurnProviders.Length > 0 ? m_ContinuousTurnProviders[0].turnSpeed : 60f;

    public static PlayerComfortRig FindInScene()
    {
        return new PlayerComfortRig
        {
            m_InputManagers = Object.FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            m_MoveProviders = Object.FindObjectsByType<DynamicMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            m_SnapTurnProviders = Object.FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            m_ContinuousTurnProviders = Object.FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            m_LocomotionProviders = Object.FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None),
        };
    }

    public ComfortSettings LoadSettings()
    {
        return ComfortSettings.Load(DefaultMoveSpeed, DefaultSnapAngle, DefaultTurnSpeed);
    }

    public void Apply(ComfortSettings settings)
    {
        foreach (var inputManager in m_InputManagers)
        {
            inputManager.independentTurning = true;
            inputManager.smoothMotionEnabled = settings.SmoothMovement;
            inputManager.smoothTurnEnabled = settings.SmoothTurning;
        }

        foreach (var moveProvider in m_MoveProviders)
            moveProvider.moveSpeed = settings.MoveSpeed;

        foreach (var snapTurnProvider in m_SnapTurnProviders)
        {
            snapTurnProvider.turnAmount = settings.SnapAngle;
            snapTurnProvider.enableTurnAround = false;
        }

        foreach (var turnProvider in m_ContinuousTurnProviders)
        {
            turnProvider.turnSpeed = settings.TurnSpeed;
            turnProvider.enableTurnAround = false;
        }

        ApplyVignette(settings);
    }

    /// <summary>
    /// Spawns the tunneling vignette prefab on the player camera and links it to the locomotion providers.
    /// </summary>
    public void CreateVignette(GameObject vignettePrefab, Transform playerCamera, ComfortSettings settings)
    {
        if (playerCamera == null || vignettePrefab == null || m_VignetteController != null)
            return;

        var vignetteObject = Object.Instantiate(vignettePrefab, playerCamera, false);
        vignetteObject.name = "Player Tunneling Vignette";
        m_VignetteController = vignetteObject.GetComponent<TunnelingVignetteController>();
        if (m_VignetteController == null)
            m_VignetteController = vignetteObject.AddComponent<TunnelingVignetteController>();

        m_VignetteProviders.Clear();
        foreach (var locomotionProvider in m_LocomotionProviders)
        {
            if (!(locomotionProvider is ContinuousMoveProvider) &&
                !(locomotionProvider is SnapTurnProvider) &&
                !(locomotionProvider is ContinuousTurnProvider) &&
                !(locomotionProvider is TeleportationProvider))
                continue;

            m_VignetteProviders.Add(new LocomotionVignetteProvider
            {
                locomotionProvider = locomotionProvider,
                enabled = true,
            });
        }

        m_VignetteController.locomotionVignetteProviders = m_VignetteProviders;
        ApplyVignette(settings);
    }

    /// <summary>
    /// Temporarily disables all locomotion (e.g. while a menu is open or during arm calibration).
    /// </summary>
    public void SuppressLocomotion()
    {
        if (m_ProviderStates.Count > 0)
            return;

        foreach (var provider in m_LocomotionProviders)
        {
            if (provider == null)
                continue;
            m_ProviderStates[provider] = provider.enabled;
            provider.enabled = false;
        }
    }

    public void RestoreLocomotion()
    {
        foreach (var providerState in m_ProviderStates)
        {
            if (providerState.Key != null)
                providerState.Key.enabled = providerState.Value;
        }

        m_ProviderStates.Clear();
    }

    void ApplyVignette(ComfortSettings settings)
    {
        if (m_VignetteController == null)
            return;

        var parameters = m_VignetteController.defaultParameters;
        parameters.apertureSize = settings.VignetteEnabled ? Mathf.Lerp(1f, 0.35f, settings.VignetteStrength) : 1f;
        m_VignetteController.defaultParameters = parameters;
    }
}
