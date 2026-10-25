using AllGunEvo.Core;
using Xunit;

namespace AllGunEvo.Core.Tests;

public sealed class DisplayPolicyTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 4)]
    [InlineData(1, 1)]
    public void NormalizeClampsVSync(int input, int expected)
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(new DisplaySettings { Enabled = true, VSyncCount = input });
        Assert.Equal(expected, normalized.VSyncCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 3)]
    [InlineData(2, 2)]
    public void NormalizeClampsQueueDepth(int input, int expected)
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(new DisplaySettings { Enabled = true, MaxQueuedFrames = input });
        Assert.Equal(expected, normalized.MaxQueuedFrames);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(-5, -1)]
    [InlineData(144, 144)]
    public void NormalizeMapsNonPositiveFrameRateToUnlimited(int input, int expected)
    {
        DisplaySettings normalized = DisplayPolicy.Normalize(new DisplaySettings { Enabled = true, TargetFrameRate = input });
        Assert.Equal(expected, normalized.TargetFrameRate);
    }

    [Fact]
    public void NoReapplyWhenStateMatches()
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
        Assert.False(DisplayPolicy.ShouldReapply(wanted, new DisplayState(1, -1, 3)));
    }

    [Theory]
    [InlineData(0, -1, 3)]
    [InlineData(1, 60, 3)]
    [InlineData(1, -1, 1)]
    public void ReapplyWhenAnyValueDrifts(int vSync, int frameRate, int queued)
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
        Assert.True(DisplayPolicy.ShouldReapply(wanted, new DisplayState(vSync, frameRate, queued)));
    }

    [Fact]
    public void DisabledNeverReapplies()
    {
        var wanted = new DisplaySettings { Enabled = false, VSyncCount = 1 };
        Assert.False(DisplayPolicy.ShouldReapply(wanted, new DisplayState(0, 60, 1)));
    }

    [Fact]
    public void DescribeNamesTheDriftingValues()
    {
        var wanted = new DisplaySettings { Enabled = true, VSyncCount = 1, TargetFrameRate = -1, MaxQueuedFrames = 3 };
        string text = DisplayPolicy.Describe(wanted, new DisplayState(0, 60, 3));
        Assert.Contains("vSync", text);
        Assert.Contains("fps", text);
        Assert.DoesNotContain("queue", text);
    }
}
