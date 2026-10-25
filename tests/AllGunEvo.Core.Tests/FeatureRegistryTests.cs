using System;
using AllGunEvo.Core;
using AllGunEvo.Core.Features;
using Xunit;

namespace AllGunEvo.Core.Tests;

internal sealed class ThrowingFeature : IAllGunEvoFeature
{
    public string Id => "test.throwing";
    public int RunCalls { get; private set; }
    public int TickCalls { get; private set; }

    public void OnRunStarted(RunContext context)
    {
        RunCalls++;
        throw new InvalidOperationException("intentional");
    }

    public void OnTick(TickContext context)
    {
        TickCalls++;
        throw new InvalidOperationException("intentional");
    }
}

internal sealed class CountingFeature : IAllGunEvoFeature
{
    public string Id { get; set; } = "test.counting";
    public int RunCalls { get; private set; }
    public int TickCalls { get; private set; }
    public int LastRunToken { get; private set; } = int.MinValue;

    public void OnRunStarted(RunContext context)
    {
        RunCalls++;
        LastRunToken = context.RunToken;
    }

    public void OnTick(TickContext context)
    {
        TickCalls++;
    }
}

public sealed class FeatureRegistryTests
{
    [Fact]
    public void RejectsDuplicateIds()
    {
        var registry = new FeatureRegistry();
        var log = new FakeLog();

        Assert.True(registry.TryRegister(new CountingFeature { Id = "dup" }, log));
        Assert.False(registry.TryRegister(new CountingFeature { Id = "dup" }, log));
        Assert.Single(registry.Snapshot());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void RejectsFeatureWithoutId()
    {
        var registry = new FeatureRegistry();
        Assert.False(registry.TryRegister(new CountingFeature { Id = "" }, new FakeLog()));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void OneThrowingFeatureDoesNotStopTheOthers()
    {
        var registry = new FeatureRegistry();
        var log = new FakeLog();
        var thrower = new ThrowingFeature();
        var counter = new CountingFeature();

        registry.TryRegister(thrower, log);
        registry.TryRegister(counter, log);

        registry.RaiseRunStarted(new RunContext(7), log);
        registry.RaiseTick(new TickContext(0.5f, true), log);

        Assert.Equal(1, thrower.RunCalls);
        Assert.Equal(1, thrower.TickCalls);
        Assert.Equal(1, counter.RunCalls);
        Assert.Equal(1, counter.TickCalls);
        Assert.Equal(7, counter.LastRunToken);
        Assert.Equal(2, log.Errors.Count);
    }
}

public sealed class BuiltInFeatureTests
{
    [Fact]
    public void EvolutionFeatureGrantsThroughPolicy()
    {
        var settings = new FakeSettings();
        var gateway = new FakeEvolutionGateway().Add("a").Add("b");
        var log = new FakeLog();
        var feature = new EvolutionFeature(new EvolutionGrantPolicy(), settings, gateway, log);

        feature.OnRunStarted(new RunContext(1));
        feature.OnRunStarted(new RunContext(1));

        Assert.Equal(2, gateway.Equipped.Count);
        Assert.Contains(log.Infos, m => m.Contains("Applied 2/2"));
        Assert.Contains(log.Infos, m => m.Contains("already granted"));
    }

    [Fact]
    public void VacuumFeatureCollectsOnlyWhenCombatTimerFires()
    {
        var settings = new FakeSettings();
        settings.Vacuum.IntervalSeconds = 10f;
        var gateway = new FakeVacuumGateway { Amount = 4 };
        var log = new FakeLog();
        var feature = new VacuumFeature(new VacuumSchedule(), settings, gateway, log);

        feature.OnTick(new TickContext(5f, true));
        Assert.Equal(0, gateway.Calls);

        feature.OnTick(new TickContext(5f, true));
        Assert.Equal(1, gateway.Calls);

        feature.OnTick(new TickContext(100f, false));
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public void DisplayFeatureWritesOnlyOnDrift()
    {
        var settings = new FakeSettings();
        var gateway = new FakeDisplayGateway { State = new DisplayState(1, -1, 3) };
        var log = new FakeLog();
        var feature = new DisplayFeature(settings, gateway, log);

        feature.OnTick(new TickContext(1f, true));
        Assert.Empty(gateway.Writes);

        gateway.State = new DisplayState(0, 60, 1);
        feature.OnTick(new TickContext(1f, true));

        Assert.Single(gateway.Writes);
        Assert.Contains(log.Infos, m => m.Contains("drift detected"));
        Assert.Equal(1, gateway.State.VSyncCount);
    }

    [Fact]
    public void DisplayFeatureForceApplyAlwaysWrites()
    {
        var settings = new FakeSettings();
        var gateway = new FakeDisplayGateway { State = new DisplayState(1, -1, 3) };
        var feature = new DisplayFeature(settings, gateway, new FakeLog());

        feature.ForceApply("test");

        Assert.Single(gateway.Writes);
    }
}
