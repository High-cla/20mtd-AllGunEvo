using System;
using BepInEx.Logging;
using UnityEngine;
using AllGunEvo.Core;

namespace AllGunEvo.Adapters;

internal sealed class UnityLogPort : ILogPort
{
    private readonly ManualLogSource source;

    internal UnityLogPort(ManualLogSource source)
    {
        this.source = source;
    }

    public void Info(string message) => source.LogInfo(message);
    public void Warn(string message) => source.LogWarning(message);
    public void Error(string message) => source.LogError(message);
}

/// <summary>
/// Writes the frame pacing values. Unity only applies vSyncCount to a freshly built swap chain,
/// so a vSync change also rebuilds it through Screen.SetResolution (same call the game's own
/// options menu uses) — guarded so it happens once per change, not per frame.
/// </summary>
internal sealed class QualitySettingsGateway : IDisplayGateway
{
    private bool swapChainRebuilt;

    public DisplayState Read()
    {
        return new DisplayState(QualitySettings.vSyncCount, Application.targetFrameRate, QualitySettings.maxQueuedFrames);
    }

    public void Write(DisplaySettings settings)
    {
        if (settings == null) return;

        int actualVSync = QualitySettings.vSyncCount;

        QualitySettings.vSyncCount = settings.VSyncCount;
        Application.targetFrameRate = settings.TargetFrameRate;
        QualitySettings.maxQueuedFrames = settings.MaxQueuedFrames;

        bool needsRebuild = settings.VSyncCount > 0 && (!swapChainRebuilt || actualVSync != settings.VSyncCount);
        if (!needsRebuild) return;

        swapChainRebuilt = true;
        RebuildSwapChain(settings.VSyncCount);
    }

    private static void RebuildSwapChain(int vSyncCount)
    {
        try
        {
            Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreen, Screen.currentResolution.refreshRate);
            AllGunEvoPlugin.LogSource?.LogWarning("swap chain rebuilt for vSync=" + vSyncCount +
                " (" + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreen +
                " refresh=" + Screen.currentResolution.refreshRate + "Hz)");
        }
        catch (Exception e)
        {
            AllGunEvoPlugin.LogSource?.LogWarning("swap chain rebuild failed: " + e);
        }
    }
}

/// <summary>
/// Reads ConfigEntry values into pre-allocated DTO instances, so the per-frame read path allocates
/// nothing while still seeing hot-reloaded config values.
/// </summary>
internal sealed class ConfigSettingsProvider : ISettingsProvider
{
    private readonly EvolutionSettings evolution = new EvolutionSettings();
    private readonly VacuumSettings vacuum = new VacuumSettings();
    private readonly DisplaySettings display = new DisplaySettings();

    public EvolutionSettings Evolution
    {
        get
        {
            evolution.Enabled = AllGunEvoPlugin.AllEvolutions.Value;
            return evolution;
        }
    }

    public VacuumSettings Vacuum
    {
        get
        {
            vacuum.Enabled = AllGunEvoPlugin.VacuumEnabled.Value;
            vacuum.IntervalSeconds = AllGunEvoPlugin.VacuumInterval.Value;
            return vacuum;
        }
    }

    public DisplaySettings Display
    {
        get
        {
            display.Enabled = AllGunEvoPlugin.DisplayEnabled.Value;
            display.VSyncCount = AllGunEvoPlugin.VSync.Value;
            display.TargetFrameRate = AllGunEvoPlugin.TargetFrameRate.Value;
            display.MaxQueuedFrames = AllGunEvoPlugin.MaxQueuedFrames.Value;
            return display;
        }
    }

    public bool VerboseLogging => AllGunEvoPlugin.Verbose.Value;
}
