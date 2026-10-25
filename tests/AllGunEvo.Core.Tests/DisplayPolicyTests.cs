using AllGunEvo.Core;
using Xunit;

namespace AllGunEvo.Core.Tests;

public sealed class DisplayPolicyTests
{
    private const int Refresh = 60;

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 4)]
    [InlineData(1, 1)]
    public void NormalizeClampsVSync(int input, int expected)
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(new DisplaySettings { Enabled = true, VSyncCount = input }, Refresh);
        Assert.Equal(expected, normalized.VSyncCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 3)]
    [InlineData(2, 2)]
    public void NormalizeClampsQueueDepth(int input, int expected)
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(new DisplaySettings { Enabled = true, MaxQueuedFrames = input }, Refresh);
        Assert.Equal(expected, normalized.MaxQueuedFrames);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(-5, -1)]
    [InlineData(144, 144)]
    public void NormalizeMapsNonPositiveFrameRateToUnlimited(int input, int expected)
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(new DisplaySettings { Enabled = true, TargetFrameRate = input }, Refresh);
        Assert.Equal(expected, normalized.TargetFrameRate);
    }

    [Fact]
    public void NoReapplyWhenStateMatches()
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
        Assert.False(DisplayPolicy.ShouldReapply(wanted, new DisplayState(1, -1, 3), Refresh));
    }

    [Theory]
    [InlineData(0, -1, 3)]
    [InlineData(1, 60, 3)]
    [InlineData(1, -1, 1)]
    public void ReapplyWhenAnyValueDrifts(int vSync, int frameRate, int queued)
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
        Assert.True(DisplayPolicy.ShouldReapply(wanted, new DisplayState(vSync, frameRate, queued), Refresh));
    }

    [Fact]
    public void DisabledNeverReapplies()
    {
        var wanted = new DisplaySettings { Enabled = false, VSyncCount = 1 };
        Assert.False(DisplayPolicy.ShouldReapply(wanted, new DisplayState(0, 60, 1), Refresh));
    }

    [Fact]
    public void DescribeNamesTheDriftingValues()
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
        string text = DisplayPolicy.Describe(wanted, new DisplayState(0, 60, 3), Refresh);
        Assert.Contains("vSync", text);
        Assert.Contains("fps", text);
        Assert.DoesNotContain("queue", text);
    }

    // ---- soft frame limit ----

    private static DisplaySettings SoftLimit(int offset = 3) => new DisplaySettings
    {
        Enabled = true,
        VSyncCount = 1,
        TargetFrameRate = -1,
        MaxQueuedFrames = 3,
        SoftLimitToRefresh = true,
        SoftLimitOffset = offset
    };

    [Theory]
    [InlineData(60, 3, 57)]
    [InlineData(144, 3, 141)]
    [InlineData(165, 3, 162)]
    [InlineData(30, 40, 1)]
    public void SoftLimitComputesRefreshMinusOffset(int refresh, int offset, int expected)
    {
        Assert.Equal(expected, DisplayPolicy.ComputeTargetFrameRate(SoftLimit(offset), refresh));
    }

    [Fact]
    public void SoftLimitFallsBackWhenRefreshUnknown()
    {
        var wanted = SoftLimit();
        Assert.Equal(-1, DisplayPolicy.ComputeTargetFrameRate(wanted, 0));
    }

    [Fact]
    public void SoftLimitForcesVSyncOff()
    {
        Assert.Equal(0, DisplayPolicy.ComputeVSyncCount(SoftLimit()));
    }

    [Fact]
    public void SoftLimitNormalizeProducesEffectiveValues()
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(SoftLimit(), Refresh);
        Assert.Equal(0, normalized.VSyncCount);
        Assert.Equal(57, normalized.TargetFrameRate);
        Assert.True(normalized.SoftLimitToRefresh);
    }

    [Fact]
    public void SoftLimitDetectsDriftAgainstVSyncState()
    {
        // Engine currently has vSync=1/unlimited, but the soft limit wants vSync=0/57 -> must reapply.
        Assert.True(DisplayPolicy.ShouldReapply(SoftLimit(), new DisplayState(1, -1, 3), Refresh));
        Assert.False(DisplayPolicy.ShouldReapply(SoftLimit(), new DisplayState(0, 57, 3), Refresh));
    }

    [Fact]
    public void SoftLimitDescriptionMentionsComputedRate()
    {
        string text = DisplayPolicy.DescribeSoftLimit(SoftLimit(), Refresh);
        Assert.Contains("60-3", text);
        Assert.Contains("57fps", text);
    }

    [Fact]
    public void SoftLimitDescriptionEmptyWhenDisabled()
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1 };
        Assert.Equal(string.Empty, DisplayPolicy.DescribeSoftLimit(wanted, Refresh));
    }
}
