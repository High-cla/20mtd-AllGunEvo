using System;

namespace AllGunEvo.Core;

/// <summary>Opaque handle to one gun evolution. The core never inspects <see cref="Native"/>.</summary>
public readonly struct EvolutionHandle
{
    public EvolutionHandle(string name, object native)
    {
        Name = name ?? string.Empty;
        Native = native;
    }

    public string Name { get; }
    public object Native { get; }
}

/// <summary>Driven port: everything the core needs from a gun's evolution list.</summary>
public interface IEvolutionGateway
{
    string GunName { get; }
    int Count { get; }
    EvolutionHandle Get(int index);
    void Equip(EvolutionHandle handle);
}

/// <summary>Driven port: collect every XP pickup on the map, returning how many were taken.</summary>
public interface IVacuumGateway
{
    int CollectAll();
}

public interface ILogPort
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

/// <summary>Snapshot of the engine's live frame pacing values.</summary>
public readonly struct DisplayState
{
    public DisplayState(int vSyncCount, int targetFrameRate, int maxQueuedFrames)
    {
        VSyncCount = vSyncCount;
        TargetFrameRate = targetFrameRate;
        MaxQueuedFrames = maxQueuedFrames;
    }

    public int VSyncCount { get; }
    public int TargetFrameRate { get; }
    public int MaxQueuedFrames { get; }
}

/// <summary>Driven port: read/write the engine's global frame pacing state.</summary>
public interface IDisplayGateway
{
    /// <summary>Monitor refresh rate in Hz, or 0 when the engine cannot report it.</summary>
    int RefreshRate { get; }

    DisplayState Read();
    void Write(DisplaySettings settings);
}

/// <summary>Driven port: settings values, re-read on every access so hot reload needs no plumbing.</summary>
public interface ISettingsProvider
{
    EvolutionSettings Evolution { get; }
    VacuumSettings Vacuum { get; }
    DisplaySettings Display { get; }

    /// <summary>Diagnostic sink: log live display values once per second.</summary>
    bool VerboseLogging { get; }
}

public sealed class NullLogPort : ILogPort
{
    public static readonly NullLogPort Instance = new NullLogPort();

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}
