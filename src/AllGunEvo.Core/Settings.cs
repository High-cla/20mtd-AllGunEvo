namespace AllGunEvo.Core;

/// <summary>Pure settings DTO. The adapter layer is the only place that knows about ConfigEntry.</summary>
public sealed class EvolutionSettings
{
    public bool Enabled { get; set; }
}

public sealed class VacuumSettings
{
    public bool Enabled { get; set; }
    public float IntervalSeconds { get; set; } = 30f;
}

public sealed class DisplaySettings
{
    public bool Enabled { get; set; } = true;
    public int VSyncCount { get; set; } = 1;
    public int TargetFrameRate { get; set; } = -1;
    public int MaxQueuedFrames { get; set; } = 3;
}
