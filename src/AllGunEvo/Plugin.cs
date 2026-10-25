using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using AllGunEvo.Features;
using AllGunEvo.Support;

namespace AllGunEvo;

[BepInPlugin(Guid, "All Gun Evolutions + XP Vacuum + Display", Version)]
public sealed class AllGunEvoPlugin : BaseUnityPlugin
{
    public const string Guid = "local.20mtd.allgunevo";
    public const string Version = "1.2.0";

    internal static ManualLogSource Log;

    internal static ConfigEntry<bool> AllEvolutions;
    internal static ConfigEntry<bool> VacuumEnabled;
    internal static ConfigEntry<float> VacuumInterval;

    internal static ConfigEntry<bool> DisplayEnabled;
    internal static ConfigEntry<int> VSync;
    internal static ConfigEntry<int> TargetFrameRate;
    internal static ConfigEntry<int> MaxQueuedFrames;
    internal static ConfigEntry<bool> Verbose;

    private void Awake()
    {
        Log = Logger;
        BindConfig();
        GameApi.Validate();

        Harmony harmony = new Harmony(Guid);
        Patch(harmony, typeof(GunEvolutionFeature));
        Patch(harmony, typeof(OptionsResolutionPatch));

        gameObject.AddComponent<XpVacuumRunner>();
        gameObject.AddComponent<DisplayRunner>();
        DisplayTuning.Attach();

        Log.LogInfo("AllGunEvo " + Version + " loaded.");
    }

    private static void Patch(Harmony harmony, Type patchClass)
    {
        try
        {
            harmony.PatchAll(patchClass);
        }
        catch (Exception e)
        {
            Log.LogError("patch " + patchClass.Name + " failed: " + e);
        }
    }

    private void BindConfig()
    {
        AllEvolutions = Config.Bind("Evolutions", "GiveAllOnRunStart", true,
            "Apply every gun evolution of the selected gun at run start. The level-20 evolution menu still appears.");

        VacuumEnabled = Config.Bind("XPVacuum", "Enabled", true,
            "Every IntervalSeconds in combat, hand every XP pickup on the map to the player.");
        VacuumInterval = Config.Bind("XPVacuum", "IntervalSeconds", 30f,
            "Seconds between XP vacuums.");

        DisplayEnabled = Config.Bind("Display", "Enabled", true,
            "Apply the frame pacing settings below. Disable to leave the game untouched.");
        VSync = Config.Bind("Display", "VSyncCount", 1,
            "0 = off (lowest latency, tearing). 1 = follow monitor refresh. 2-4 = every Nth refresh.");
        TargetFrameRate = Config.Bind("Display", "TargetFrameRate", -1,
            "-1 = unlimited. Unity ignores this whenever VSyncCount is not 0.");
        MaxQueuedFrames = Config.Bind("Display", "MaxQueuedFrames", 3,
            "Render queue depth, the Unity-native analogue of Reflex low-latency mode. 1 = lowest latency, 3 = Unity default.");
        Verbose = Config.Bind("Display", "Verbose", false,
            "Log the live frame pacing values once per second (diagnostics).");
    }
}
