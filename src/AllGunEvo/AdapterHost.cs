using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using flanne;
using flanne.Core;
using AllGunEvo.Adapters;
using AllGunEvo.Core;
using AllGunEvo.Core.Features;

namespace AllGunEvo;

/// <summary>Composition root: builds the ports, registers the features, forwards engine events.</summary>
internal static class AdapterHost
{
    /// <summary>Scene-wide lookups are expensive; retry at most once per second while the controller is missing.</summary>
    private const float ControllerProbeSeconds = 1f;

    internal static ILogPort Log;
    internal static GameApi Api;
    internal static ISettingsProvider Settings;
    internal static FeatureRegistry Registry;
    internal static DisplayFeature Display;

    private static GameController controller;
    private static float nextControllerProbe;

    internal static void Start(ManualLogSource source)
    {
        Log = new UnityLogPort(source);
        Settings = new ConfigSettingsProvider();
        Api = new GameApi(Log);
        Registry = AllGunEvoApi.Registry;
        Display = new DisplayFeature(Settings, new QualitySettingsGateway(), Log);

        Registry.TryRegister(new EvolutionFeature(new EvolutionGrantPolicy(), Settings, Api, Log), Log);
        Registry.TryRegister(new VacuumFeature(new VacuumSchedule(), Settings, Api, Log), Log);
        Registry.TryRegister(Display, Log);

        Display.ForceApply("startup");
    }

    internal static void OnRunStarted(PlayerController player)
    {
        if (player == null) return;
        Api.BindRun(player);
        Registry.RaiseRunStarted(new RunContext(player.GetInstanceID()), Log);
    }

    internal static void OnUpdate(float deltaSeconds)
    {
        Registry.RaiseTick(new TickContext(deltaSeconds, IsCombat()), Log);
    }

    internal static void Redisplay(string reason)
    {
        Display?.ForceApply(reason);
    }

    internal static void OnSceneLoaded(Scene scene)
    {
        controller = null;
        nextControllerProbe = 0f;
        Api.BindRun(null);
        Redisplay("scene " + scene.name);
    }

    private static bool IsCombat()
    {
        if (controller == null)
        {
            if (Time.unscaledTime < nextControllerProbe) return false;
            nextControllerProbe = Time.unscaledTime + ControllerProbeSeconds;
            controller = Object.FindObjectOfType<GameController>();
        }

        return GameApi.IsCombat(controller);
    }
}
