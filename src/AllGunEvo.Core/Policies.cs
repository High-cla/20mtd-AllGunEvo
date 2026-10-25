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
/// Pure frame pacing policy. Every method works on raw ints so the per-frame path allocates nothing.
///
/// Soft limit: vSync is the only true presentation sync, but Unity ignores targetFrameRate whenever
/// vSyncCount != 0, and a vSync write only takes effect when the swap chain is rebuilt. When the
/// engine refuses to present-sync, capping a few frames under the refresh rate is the next best
/// thing: every frame still lands inside one refresh window, so pacing stays even without tearing
/// at the awkward "exactly refresh rate" boundary.
/// </summary>
public static class DisplayPolicy
{
    public const int MinVSyncCount = 0;
    public const int MaxVSyncCount = 4;
    public const int MinQueuedFrames = 1;
    public const int MaxQueuedFrames = 3;
    public const int MinFrameRate = 1;

    public static int NormalizeVSyncCount(int value) => Clamp(value, MinVSyncCount, MaxVSyncCount);

    public static int NormalizeFrameRate(int value) => value <= 0 ? -1 : value;

    public static int NormalizeQueuedFrames(int value) => Clamp(value, MinQueuedFrames, MaxQueuedFrames);

    /// <summary>Effective vSync value: the soft limit needs targetFrameRate, so it forces vSync off.</summary>
    public static int ComputeVSyncCount(DisplaySettings wanted)
    {
        if (wanted == null) return 0;
        if (wanted.SoftLimitToRefresh) return 0;
        return NormalizeVSyncCount(wanted.VSyncCount);
    }

    /// <summary>Effective frame-rate cap; refreshRate &lt;= 0 means the engine could not report one.</summary>
    public static int ComputeTargetFrameRate(DisplaySettings wanted, int refreshRate)
    {
        if (wanted == null) return -1;

        if (wanted.SoftLimitToRefresh && refreshRate > 0)
        {
            int offset = wanted.SoftLimitOffset < 0 ? 0 : wanted.SoftLimitOffset;
            return Math.Max(MinFrameRate, refreshRate - offset);
        }

        return NormalizeFrameRate(wanted.TargetFrameRate);
    }

    /// <summary>Materialises a normalised copy; only the write path uses it (low frequency).</summary>
    public static DisplaySettings Normalize(DisplaySettings wanted, int refreshRate)
    {
        if (wanted == null) return new DisplaySettings { Enabled = false };

        return new DisplaySettings
        {
            Enabled = wanted.Enabled,
            VSyncCount = ComputeVSyncCount(wanted),
            TargetFrameRate = ComputeTargetFrameRate(wanted, refreshRate),
            MaxQueuedFrames = NormalizeQueuedFrames(wanted.MaxQueuedFrames),
            SoftLimitToRefresh = wanted.SoftLimitToRefresh,
            SoftLimitOffset = wanted.SoftLimitOffset
        };
    }

    public static bool ShouldReapply(DisplaySettings wanted, DisplayState actual, int refreshRate)
    {
        if (wanted == null || !wanted.Enabled) return false;

        return actual.VSyncCount != ComputeVSyncCount(wanted)
            || actual.TargetFrameRate != ComputeTargetFrameRate(wanted, refreshRate)
            || actual.MaxQueuedFrames != NormalizeQueuedFrames(wanted.MaxQueuedFrames);
    }

    public static string Describe(DisplaySettings wanted, DisplayState actual, int refreshRate)
    {
        if (wanted == null) return string.Empty;

        int wantedVSync = ComputeVSyncCount(wanted);
        int wantedFrameRate = ComputeTargetFrameRate(wanted, refreshRate);
        int wantedQueued = NormalizeQueuedFrames(wanted.MaxQueuedFrames);

        string drift = string.Empty;
        if (actual.VSyncCount != wantedVSync) drift += " vSync " + actual.VSyncCount + "->" + wantedVSync;
        if (actual.TargetFrameRate != wantedFrameRate) drift += " fps " + actual.TargetFrameRate + "->" + wantedFrameRate;
        if (actual.MaxQueuedFrames != wantedQueued) drift += " queue " + actual.MaxQueuedFrames + "->" + wantedQueued;
        return drift.Trim();
    }

    public static string DescribeSoftLimit(DisplaySettings wanted, int refreshRate)
    {
        if (wanted == null || !wanted.SoftLimitToRefresh) return string.Empty;
        return "soft limit " + refreshRate + "-" + wanted.SoftLimitOffset +
            " = " + ComputeTargetFrameRate(wanted, refreshRate) + "fps";
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
