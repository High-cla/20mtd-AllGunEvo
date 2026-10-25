using System;

namespace AllGunEvo.Core;

public enum GrantOutcome
{
    Granted,
    AlreadyGranted,
    Disabled
}

public readonly struct GrantResult
{
    private GrantResult(GrantOutcome outcome, int applied, int total, string gunName)
    {
        Outcome = outcome;
        Applied = applied;
        Total = total;
        GunName = gunName ?? string.Empty;
    }

    public GrantOutcome Outcome { get; }
    public int Applied { get; }
    public int Total { get; }
    public string GunName { get; }

    public static GrantResult Granted(int applied, int total, string gunName) => new GrantResult(GrantOutcome.Granted, applied, total, gunName);
    public static GrantResult AlreadyGranted() => new GrantResult(GrantOutcome.AlreadyGranted, 0, 0, string.Empty);
    public static GrantResult Disabled() => new GrantResult(GrantOutcome.Disabled, 0, 0, string.Empty);
}

/// <summary>
/// Decides whether this run still needs its evolutions, and applies them one by one so a single
/// failing evolution cannot take the others down. Pure: all engine access goes through the ports.
/// </summary>
public sealed class EvolutionGrantPolicy
{
    private int lastRunToken = int.MinValue;

    public int LastRunToken => lastRunToken;

    public GrantResult Apply(int runToken, EvolutionSettings settings, IEvolutionGateway gateway, ILogPort log)
    {
        if (settings == null || !settings.Enabled) return GrantResult.Disabled();
        if (runToken == lastRunToken) return GrantResult.AlreadyGranted();

        lastRunToken = runToken;
        if (gateway == null) return GrantResult.Granted(0, 0, string.Empty);

        int total = gateway.Count;
        int applied = 0;
        for (int i = 0; i < total; i++)
        {
            EvolutionHandle handle = gateway.Get(i);
            try
            {
                gateway.Equip(handle);
                applied++;
            }
            catch (Exception e)
            {
                log?.Error("evolution " + i + " (" + handle.Name + ") failed: " + e);
            }
        }

        return GrantResult.Granted(applied, total, gateway.GunName);
    }
}

/// <summary>Interval accumulation for the XP vacuum. Combat gating and interval clamping are policy, not adapter detail.</summary>
public sealed class VacuumSchedule
{
    public const float MinimumIntervalSeconds = 1f;

    private float elapsed;

    public float Elapsed => elapsed;

    public bool Tick(float deltaSeconds, bool inCombat, VacuumSettings settings)
    {
        if (settings == null || !settings.Enabled)
        {
            elapsed = 0f;
            return false;
        }

        if (!inCombat)
        {
            elapsed = 0f;
            return false;
        }

        float interval = settings.IntervalSeconds < MinimumIntervalSeconds
            ? MinimumIntervalSeconds
            : settings.IntervalSeconds;

        if (deltaSeconds > 0f) elapsed += deltaSeconds;
        if (elapsed < interval) return false;

        // Restart the countdown instead of carrying the remainder: a long stall must not chain-fire
        // several vacuums in a row, and the pickups it would catch are already gone.
        elapsed = 0f;
        return true;
    }

    public void Reset() => elapsed = 0f;
}

/// <summary>
/// Pure comparison between configured and live frame pacing values. The per-frame path
/// (<see cref="ShouldReapply"/>) works on raw ints so it allocates nothing.
/// </summary>
public static class DisplayPolicy
{
    public const int MinVSyncCount = 0;
    public const int MaxVSyncCount = 4;
    public const int MinQueuedFrames = 1;
    public const int MaxQueuedFrames = 3;

    public static int NormalizeVSyncCount(int value) => Clamp(value, MinVSyncCount, MaxVSyncCount);

    public static int NormalizeFrameRate(int value) => value <= 0 ? -1 : value;

    public static int NormalizeQueuedFrames(int value) => Clamp(value, MinQueuedFrames, MaxQueuedFrames);

    /// <summary>Materialises a normalised copy; only the write path uses it (low frequency).</summary>
    public static DisplaySettings Normalize(DisplaySettings wanted)
    {
        if (wanted == null) return new DisplaySettings { Enabled = false };

        return new DisplaySettings
        {
            Enabled = wanted.Enabled,
            VSyncCount = NormalizeVSyncCount(wanted.VSyncCount),
            TargetFrameRate = NormalizeFrameRate(wanted.TargetFrameRate),
            MaxQueuedFrames = NormalizeQueuedFrames(wanted.MaxQueuedFrames)
        };
    }

    public static bool ShouldReapply(DisplaySettings wanted, DisplayState actual)
    {
        if (wanted == null || !wanted.Enabled) return false;

        return actual.VSyncCount != NormalizeVSyncCount(wanted.VSyncCount)
            || actual.TargetFrameRate != NormalizeFrameRate(wanted.TargetFrameRate)
            || actual.MaxQueuedFrames != NormalizeQueuedFrames(wanted.MaxQueuedFrames);
    }

    public static string Describe(DisplaySettings wanted, DisplayState actual)
    {
        if (wanted == null) return string.Empty;

        int wantedVSync = NormalizeVSyncCount(wanted.VSyncCount);
        int wantedFrameRate = NormalizeFrameRate(wanted.TargetFrameRate);
        int wantedQueued = NormalizeQueuedFrames(wanted.MaxQueuedFrames);

        string drift = string.Empty;
        if (actual.VSyncCount != wantedVSync) drift += " vSync " + actual.VSyncCount + "->" + wantedVSync;
        if (actual.TargetFrameRate != wantedFrameRate) drift += " fps " + actual.TargetFrameRate + "->" + wantedFrameRate;
        if (actual.MaxQueuedFrames != wantedQueued) drift += " queue " + actual.MaxQueuedFrames + "->" + wantedQueued;
        return drift.Trim();
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
