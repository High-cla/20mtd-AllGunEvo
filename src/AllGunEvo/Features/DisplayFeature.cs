using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using flanne.UI;

namespace AllGunEvo.Features;

/// <summary>
/// Frame pacing and latency knobs. Driver-side Reflex / Anti-Lag 2 / XeLL need in-game markers,
/// so the Unity-native equivalents are frame-rate cap, vSync and render queue depth.
/// QualitySettings is global mutable state: any Screen.SetResolution / SetQualityLevel resets it,
/// so every write here is idempotent and re-applied on scene load and on detected drift.
/// </summary>
internal static class DisplayTuning
{
    private static float nextDriftCheck;

    internal static void Attach()
    {
        LogState("game default");
        Apply("startup");

        AllGunEvoPlugin.DisplayEnabled.SettingChanged += OnSettingChanged;
        AllGunEvoPlugin.VSync.SettingChanged += OnSettingChanged;
        AllGunEvoPlugin.TargetFrameRate.SettingChanged += OnSettingChanged;
        AllGunEvoPlugin.MaxQueuedFrames.SettingChanged += OnSettingChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    internal static void Tick()
    {
        if (!AllGunEvoPlugin.DisplayEnabled.Value) return;
        if (Time.unscaledTime < nextDriftCheck) return;
        nextDriftCheck = Time.unscaledTime + 1f;

        if (Drifted()) Apply("drift corrected");
        else if (AllGunEvoPlugin.Verbose.Value) LogState("tick");
    }

    internal static void Apply(string reason)
    {
        if (!AllGunEvoPlugin.DisplayEnabled.Value) return;

        QualitySettings.vSyncCount = Mathf.Clamp(AllGunEvoPlugin.VSync.Value, 0, 4);
        int frameRate = AllGunEvoPlugin.TargetFrameRate.Value;
        Application.targetFrameRate = frameRate <= 0 ? -1 : frameRate;
        QualitySettings.maxQueuedFrames = Mathf.Clamp(AllGunEvoPlugin.MaxQueuedFrames.Value, 1, 3);

        LogState("applied (" + reason + ")");
    }

    private static bool Drifted()
    {
        int wantedVSync = Mathf.Clamp(AllGunEvoPlugin.VSync.Value, 0, 4);
        int wantedFps = AllGunEvoPlugin.TargetFrameRate.Value <= 0 ? -1 : AllGunEvoPlugin.TargetFrameRate.Value;
        int wantedQueued = Mathf.Clamp(AllGunEvoPlugin.MaxQueuedFrames.Value, 1, 3);

        return QualitySettings.vSyncCount != wantedVSync
            || Application.targetFrameRate != wantedFps
            || QualitySettings.maxQueuedFrames != wantedQueued;
    }

    private static void OnSettingChanged(object sender, EventArgs e) => Apply("config changed");

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply("scene " + scene.name);

    private static void LogState(string when)
    {
        AllGunEvoPlugin.Log.LogInfo("display " + when +
            ": vSync=" + QualitySettings.vSyncCount +
            " targetFrameRate=" + Application.targetFrameRate +
            " maxQueuedFrames=" + QualitySettings.maxQueuedFrames +
            " refresh=" + Screen.currentResolution.refreshRate + "Hz" +
            " fullscreen=" + Screen.fullScreen);
    }
}

/// <summary>
/// The game rebuilds its swap chain exactly here (flanne.UI/OptionsSetter.cs SetFullscreen/SetResolution),
/// and Unity resets QualitySettings.vSyncCount when the chain is rebuilt, so re-apply right after.
/// </summary>
[HarmonyPatch]
internal static class OptionsResolutionPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionsSetter), "SetFullscreen")]
    private static void AfterFullscreen()
    {
        DisplayTuning.Apply("options fullscreen");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionsSetter), "SetResolution")]
    private static void AfterResolution()
    {
        DisplayTuning.Apply("options resolution");
    }
}

/// <summary>Cheap driver for the drift watchdog (runs once per frame, work is throttled to 1 Hz).</summary>
internal sealed class DisplayRunner : MonoBehaviour
{
    private void Update()
    {
        DisplayTuning.Tick();
    }
}
