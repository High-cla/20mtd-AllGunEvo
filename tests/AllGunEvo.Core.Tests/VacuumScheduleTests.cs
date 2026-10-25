using AllGunEvo.Core;
using Xunit;

namespace AllGunEvo.Core.Tests;

public sealed class VacuumScheduleTests
{
    private static VacuumSettings Thirty() => new VacuumSettings { Enabled = true, IntervalSeconds = 30f };

    [Fact]
    public void DoesNotFireBeforeInterval()
    {
        var schedule = new VacuumSchedule();
        Assert.False(schedule.Tick(29f, true, Thirty()));
        Assert.True(schedule.Tick(1f, true, Thirty()));
    }

    [Fact]
    public void FiresOncePerIntervalAndKeepsRemainder()
    {
        var schedule = new VacuumSchedule();
        Assert.True(schedule.Tick(30f, true, Thirty()));
        Assert.False(schedule.Tick(29f, true, Thirty()));
        Assert.True(schedule.Tick(1f, true, Thirty()));
    }

    [Fact]
    public void SingleHugeDeltaFiresOnlyOnce()
    {
        var schedule = new VacuumSchedule();
        Assert.True(schedule.Tick(300f, true, Thirty()));
        Assert.False(schedule.Tick(0f, true, Thirty()));
    }

    [Fact]
    public void OutOfCombatResetsAccumulator()
    {
        var schedule = new VacuumSchedule();
        Assert.False(schedule.Tick(25f, true, Thirty()));
        Assert.False(schedule.Tick(25f, false, Thirty()));
        Assert.Equal(0f, schedule.Elapsed);
        Assert.False(schedule.Tick(25f, true, Thirty()));
    }

    [Fact]
    public void DisabledResetsAccumulator()
    {
        var schedule = new VacuumSchedule();
        Assert.False(schedule.Tick(25f, true, Thirty()));
        Assert.False(schedule.Tick(25f, true, new VacuumSettings { Enabled = false, IntervalSeconds = 30f }));
        Assert.Equal(0f, schedule.Elapsed);
    }

    [Fact]
    public void IntervalIsClampedToMinimum()
    {
        var schedule = new VacuumSchedule();
        var settings = new VacuumSettings { Enabled = true, IntervalSeconds = 0.1f };
        Assert.False(schedule.Tick(0.5f, true, settings));
        Assert.True(schedule.Tick(0.5f, true, settings));
    }

    [Fact]
    public void NegativeDeltaIsIgnored()
    {
        var schedule = new VacuumSchedule();
        Assert.False(schedule.Tick(-100f, true, Thirty()));
        Assert.Equal(0f, schedule.Elapsed);
    }
}
