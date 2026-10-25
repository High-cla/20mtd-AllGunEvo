using AllGunEvo.Core;
using Xunit;

namespace AllGunEvo.Core.Tests;

public sealed class EvolutionGrantPolicyTests
{
    private static EvolutionSettings On() => new EvolutionSettings { Enabled = true };

    [Fact]
    public void GrantsEveryEvolutionOnFirstRun()
    {
        var gateway = new FakeEvolutionGateway().Add("a").Add("b").Add("c");
        var policy = new EvolutionGrantPolicy();

        GrantResult result = policy.Apply(11, On(), gateway, new FakeLog());

        Assert.Equal(GrantOutcome.Granted, result.Outcome);
        Assert.Equal(3, result.Applied);
        Assert.Equal(3, result.Total);
        Assert.Equal("TestGun", result.GunName);
        Assert.Equal(new[] { "a", "b", "c" }, gateway.Equipped);
    }

    [Fact]
    public void SecondEntryInSameRunIsSkipped()
    {
        var gateway = new FakeEvolutionGateway().Add("a");
        var policy = new EvolutionGrantPolicy();

        policy.Apply(11, On(), gateway, new FakeLog());
        GrantResult second = policy.Apply(11, On(), gateway, new FakeLog());

        Assert.Equal(GrantOutcome.AlreadyGranted, second.Outcome);
        Assert.Single(gateway.Equipped);
    }

    [Fact]
    public void NewRunTokenGrantsAgain()
    {
        var gateway = new FakeEvolutionGateway().Add("a");
        var policy = new EvolutionGrantPolicy();

        policy.Apply(11, On(), gateway, new FakeLog());
        GrantResult second = policy.Apply(12, On(), gateway, new FakeLog());

        Assert.Equal(GrantOutcome.Granted, second.Outcome);
        Assert.Equal(2, gateway.Equipped.Count);
    }

    [Fact]
    public void DisabledSkipsGatewayEntirely()
    {
        var gateway = new FakeEvolutionGateway().Add("a");
        var policy = new EvolutionGrantPolicy();

        GrantResult result = policy.Apply(11, new EvolutionSettings { Enabled = false }, gateway, new FakeLog());

        Assert.Equal(GrantOutcome.Disabled, result.Outcome);
        Assert.Empty(gateway.Equipped);
        Assert.Equal(int.MinValue, policy.LastRunToken);
    }

    [Fact]
    public void OneFailingEvolutionDoesNotStopTheOthers()
    {
        var gateway = new FakeEvolutionGateway().Add("a").Add("b").Add("c");
        gateway.Throwing.Add("b");
        var log = new FakeLog();
        var policy = new EvolutionGrantPolicy();

        GrantResult result = policy.Apply(11, On(), gateway, log);

        Assert.Equal(2, result.Applied);
        Assert.Equal(3, result.Total);
        Assert.Equal(new[] { "a", "c" }, gateway.Equipped);
        Assert.Single(log.Errors);
    }

    [Fact]
    public void EmptyEvolutionListStillReportsGranted()
    {
        var gateway = new FakeEvolutionGateway();
        var policy = new EvolutionGrantPolicy();

        GrantResult result = policy.Apply(11, On(), gateway, new FakeLog());

        Assert.Equal(GrantOutcome.Granted, result.Outcome);
        Assert.Equal(0, result.Applied);
        Assert.Equal(0, result.Total);
    }
}
