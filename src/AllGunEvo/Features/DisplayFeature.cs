using System;
using UnityEngine;

namespace AllGunEvo.Features;

/// <summary>
/// Frame pacing and latency knobs. Driver-side Reflex / Anti-Lag 2 / XeLL need in-game markers,
/// so a plugin cannot reproduce them; the closest Unity-native equivalents are frame-rate cap,
/// vSync and render queue depth (maxQueuedFrames behave like Reflex low-latency queue depth).
/// </summary>
internal static class DisplayTuning
{
    internal static void Attach()
    {
        LogState("game default");
        Apply();
        AllGunEvoPlugin.DisplayEnabled.SettingChanged += OnSettingChanged;
        AllGunEvoPlugin.VSync.SettingChanged += OnSettingChanged;
        AllGunEvoPlugin.TargetFrameRate.SettingChanged += OnSettingChanged;
        AllGunEvoPlugin.MaxQueuedFrames.SettingChanged += OnSettingChanged;
    }

    private static void OnSettingChanged(object sender, EventArgs e)
    {
        Apply();
    }

    private static void Apply()
    {
        if (!AllGunEvoPlugin.DisplayEnabled.Value) return;

        QualitySettings.vSyncCount = Mathf.Clamp(AllGunEvoPlugin.VSync.Value, 0, 4);
        int frameRate = AllGunEvoPlugin.TargetFrameRate.Value;
        Application.targetFrameRate = frameRate <= 0 ? -1 : frameRate;
        QualitySettings.maxQueuedFrames = Mathf.Clamp(AllGunEvoPlugin.MaxQueuedFrames.Value, 1, 3);

        LogState("applied");
    }

    private static void LogState(string when)
    {
        AllGunEvoPlugin.Log.LogInfo("display " + when +
            ": vSync=" + QualitySettings.vSyncCount +
            " targetFrameRate=" + Application.targetFrameRate +
            " maxQueuedFrames=" + QualitySettings.maxQueuedFrames +
            " refresh=" + Screen.currentResolution.refreshRate + "Hz");
    }
}
