using System;
using System.Collections.Generic;
using AllGunEvo.Core;

namespace AllGunEvo.Core.Tests;

internal sealed class FakeEvolutionGateway : IEvolutionGateway
{
    public List<EvolutionHandle> Handles { get; } = new List<EvolutionHandle>();
    public List<string> Equipped { get; } = new List<string>();
    public HashSet<string> Throwing { get; } = new HashSet<string>();
    public string GunName { get; set; } = "TestGun";

    public FakeEvolutionGateway Add(string name)
    {
        Handles.Add(new EvolutionHandle(name, name));
        return this;
    }

    public int Count => Handles.Count;
    public EvolutionHandle Get(int index) => Handles[index];

    public void Equip(EvolutionHandle handle)
    {
        if (Throwing.Contains(handle.Name)) throw new InvalidOperationException("boom " + handle.Name);
        Equipped.Add(handle.Name);
    }
}

internal sealed class FakeVacuumGateway : IVacuumGateway
{
    public int Calls { get; private set; }
    public int Amount { get; set; } = 5;

    public int CollectAll()
    {
        Calls++;
        return Amount;
    }
}

internal sealed class FakeDisplayGateway : IDisplayGateway
{
    public int RefreshRate { get; set; } = 60;
    public DisplayState State { get; set; } = new DisplayState(3, 60, 2);
    public List<DisplaySettings> Writes { get; } = new List<DisplaySettings>();

    public DisplayState Read() => State;

    public void Write(DisplaySettings settings)
    {
        Writes.Add(settings);
        State = new DisplayState(settings.VSyncCount, settings.TargetFrameRate, settings.MaxQueuedFrames);
    }
}

internal sealed class FakeLog : ILogPort
{
    public List<string> Infos { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();
    public List<string> Errors { get; } = new List<string>();

    public void Info(string message) => Infos.Add(message);
    public void Warn(string message) => Warnings.Add(message);
    public void Error(string message) => Errors.Add(message);
}

internal sealed class FakeSettings : ISettingsProvider
{
    public EvolutionSettings Evolution { get; set; } = new EvolutionSettings { Enabled = true };
    public VacuumSettings Vacuum { get; set; } = new VacuumSettings { Enabled = true, IntervalSeconds = 30f };
    public DisplaySettings Display { get; set; } = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
    public bool VerboseLogging { get; set; }
}
