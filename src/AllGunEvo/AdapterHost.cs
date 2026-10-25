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
    internal static ILogPort Log;
    internal static GameApi Api;
    internal static ISettingsProvider Settings;
    internal static FeatureRegistry Registry;
    internal static DisplayFeature Display;

    private static GameController controller;

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
        bool inCombat = IsCombat();
        Registry.RaiseTick(new TickContext(deltaSeconds, inCombat), Log);
    }

    internal static void Redisplay(string reason)
    {
        Display?.ForceApply(reason);
    }

    internal static void OnSceneLoaded(Scene scene)
    {
        controller = null;
        Api.BindRun(null);
        Redisplay("scene " + scene.name);
    }

    private static bool IsCombat()
    {
        if (controller == null) controller = Object.FindObjectOfType<GameController>();
        return GameApi.IsCombat(controller);
    }
}
