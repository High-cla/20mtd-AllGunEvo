using System;
using System.Collections.Generic;

namespace AllGunEvo.Core;

public readonly struct RunContext
{
    public RunContext(int runToken)
    {
        RunToken = runToken;
    }

    public int RunToken { get; }
}

public readonly struct TickContext
{
    public TickContext(float deltaSeconds, bool inCombat)
    {
        DeltaSeconds = deltaSeconds;
        InCombat = inCombat;
    }

    public float DeltaSeconds { get; }
    public bool InCombat { get; }
}

/// <summary>
/// Extension contract. In-tree features and third-party BepInEx plugins enter through the very same
/// registry, so nothing about the built-in ones is privileged.
/// </summary>
public interface IAllGunEvoFeature
{
    string Id { get; }
    void OnRunStarted(RunContext context);
    void OnTick(TickContext context);
}

/// <summary>
/// Dispatch table. The feature array is rebuilt only on registration, so the per-frame dispatch
/// path takes no allocation at all.
/// </summary>
public sealed class FeatureRegistry
{
    private readonly List<IAllGunEvoFeature> features = new List<IAllGunEvoFeature>();
    private IAllGunEvoFeature[] snapshot = Array.Empty<IAllGunEvoFeature>();

    public IReadOnlyList<IAllGunEvoFeature> Snapshot() => snapshot;

    public int Count => snapshot.Length;

    public bool TryRegister(IAllGunEvoFeature feature, ILogPort log)
    {
        if (feature == null || string.IsNullOrEmpty(feature.Id))
        {
            log?.Warn("feature registration rejected: missing id");
            return false;
        }

        for (int i = 0; i < features.Count; i++)
        {
            if (string.Equals(features[i].Id, feature.Id, StringComparison.Ordinal))
            {
                log?.Warn("feature already registered: " + feature.Id);
                return false;
            }
        }

        features.Add(feature);
        snapshot = features.ToArray();
        log?.Info("feature registered: " + feature.Id);
        return true;
    }

    public void RaiseRunStarted(RunContext context, ILogPort log)
    {
        IAllGunEvoFeature[] current = snapshot;
        for (int i = 0; i < current.Length; i++)
        {
            try
            {
                current[i].OnRunStarted(context);
            }
            catch (Exception e)
            {
                log?.Error("feature " + current[i].Id + " OnRunStarted failed: " + e);
            }
        }
    }

    public void RaiseTick(TickContext context, ILogPort log)
    {
        IAllGunEvoFeature[] current = snapshot;
        for (int i = 0; i < current.Length; i++)
        {
            try
            {
                current[i].OnTick(context);
            }
            catch (Exception e)
            {
                log?.Error("feature " + current[i].Id + " OnTick failed: " + e);
            }
        }
    }
}
