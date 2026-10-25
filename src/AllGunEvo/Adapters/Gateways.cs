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

internal sealed class QualitySettingsGateway : IDisplayGateway
{
    public DisplayState Read()
    {
        return new DisplayState(QualitySettings.vSyncCount, Application.targetFrameRate, QualitySettings.maxQueuedFrames);
    }

    public void Write(DisplaySettings settings)
    {
        if (settings == null) return;
        QualitySettings.vSyncCount = settings.VSyncCount;
        Application.targetFrameRate = settings.TargetFrameRate;
        QualitySettings.maxQueuedFrames = settings.MaxQueuedFrames;
    }
}

/// <summary>Reads ConfigEntry values on demand, so hot reload needs no change notification.</summary>
internal sealed class ConfigSettingsProvider : ISettingsProvider
{
    public EvolutionSettings Evolution => new EvolutionSettings
    {
        Enabled = AllGunEvoPlugin.AllEvolutions.Value
    };

    public VacuumSettings Vacuum => new VacuumSettings
    {
        Enabled = AllGunEvoPlugin.VacuumEnabled.Value,
        IntervalSeconds = AllGunEvoPlugin.VacuumInterval.Value
    };

    public DisplaySettings Display => new DisplaySettings
    {
        Enabled = AllGunEvoPlugin.DisplayEnabled.Value,
        VSyncCount = AllGunEvoPlugin.VSync.Value,
        TargetFrameRate = AllGunEvoPlugin.TargetFrameRate.Value,
        MaxQueuedFrames = AllGunEvoPlugin.MaxQueuedFrames.Value
    };
}
