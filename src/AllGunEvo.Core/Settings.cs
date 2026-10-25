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

    /// <summary>Cap the frame rate slightly under the monitor refresh rate instead of relying on vSync.</summary>
    public bool SoftLimitToRefresh { get; set; }

    /// <summary>How far under the refresh rate the soft limit sits. A small gap keeps frame pacing steady.</summary>
    public int SoftLimitOffset { get; set; } = 3;
}
