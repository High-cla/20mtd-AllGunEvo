namespace AllGunEvo.Core.Features;

/// <summary>Grants every gun evolution once per run. Wraps EvolutionGrantPolicy.</summary>
public sealed class EvolutionFeature : IAllGunEvoFeature
{
    private readonly EvolutionGrantPolicy policy;
    private readonly ISettingsProvider settings;
    private readonly IEvolutionGateway gateway;
    private readonly ILogPort log;

    public EvolutionFeature(EvolutionGrantPolicy policy, ISettingsProvider settings, IEvolutionGateway gateway, ILogPort log)
    {
        this.policy = policy;
        this.settings = settings;
        this.gateway = gateway;
        this.log = log;
    }

    public string Id => "core.evolutions";

    public void OnRunStarted(RunContext context)
    {
        GrantResult result = policy.Apply(context.RunToken, settings.Evolution, gateway, log);
        if (result.Outcome == GrantOutcome.Disabled) return;

        if (result.Outcome == GrantOutcome.AlreadyGranted)
        {
            log?.Info("Evolutions already granted for this run; skipping.");
            return;
        }

        log?.Info("Applied " + result.Applied + "/" + result.Total + " evolutions for gun " + result.GunName + ".");
    }

    public void OnTick(TickContext context)
    {
    }
}

/// <summary>Turns the interval decision into a vacuum call. Wraps VacuumSchedule.</summary>
public sealed class VacuumFeature : IAllGunEvoFeature
{
    private readonly VacuumSchedule schedule;
    private readonly ISettingsProvider settings;
    private readonly IVacuumGateway gateway;
    private readonly ILogPort log;

    public VacuumFeature(VacuumSchedule schedule, ISettingsProvider settings, IVacuumGateway gateway, ILogPort log)
    {
        this.schedule = schedule;
        this.settings = settings;
        this.gateway = gateway;
        this.log = log;
    }

    public string Id => "core.xp-vacuum";

    public void OnRunStarted(RunContext context)
    {
        schedule.Reset();
    }

    public void OnTick(TickContext context)
    {
        if (!schedule.Tick(context.DeltaSeconds, context.InCombat, settings.Vacuum)) return;

        int collected = gateway.CollectAll();
        log?.Info("Vacuum collected " + collected + " xp pickups.");
    }
}

/// <summary>Keeps the engine's global frame pacing in line with the configured values.</summary>
public sealed class DisplayFeature : IAllGunEvoFeature
{
    public const float VerboseIntervalSeconds = 1f;

    private readonly ISettingsProvider settings;
    private readonly IDisplayGateway gateway;
    private readonly ILogPort log;

    private float verboseElapsed;

    public DisplayFeature(ISettingsProvider settings, IDisplayGateway gateway, ILogPort log)
    {
        this.settings = settings;
        this.gateway = gateway;
        this.log = log;
    }

    public string Id => "core.display";

    public void OnRunStarted(RunContext context)
    {
        ForceApply("run started");
    }

    public void OnTick(TickContext context)
    {
        DisplaySettings wanted = settings.Display;
        if (wanted == null || !wanted.Enabled) return;

        DisplayState actual = gateway.Read();
        if (DisplayPolicy.ShouldReapply(wanted, actual))
        {
            log?.Info("display drift detected: " + DisplayPolicy.Describe(wanted, actual));
            ForceApply("drift corrected");
            return;
        }

        if (!settings.VerboseLogging) return;

        verboseElapsed += context.DeltaSeconds > 0f ? context.DeltaSeconds : 0f;
        if (verboseElapsed < VerboseIntervalSeconds) return;

        verboseElapsed = 0f;
        log?.Info("display ok: vSync=" + actual.VSyncCount +
            " targetFrameRate=" + actual.TargetFrameRate +
            " maxQueuedFrames=" + actual.MaxQueuedFrames);
    }

    /// <summary>Applies unconditionally; used for startup, config changes, scene loads and drift repair.</summary>
    public void ForceApply(string reason)
    {
        DisplaySettings wanted = settings.Display;
        if (wanted == null || !wanted.Enabled) return;

        gateway.Write(DisplayPolicy.Normalize(wanted));

        DisplayState actual = gateway.Read();
        log?.Info("display applied (" + reason + "): vSync=" + actual.VSyncCount +
            " targetFrameRate=" + actual.TargetFrameRate +
            " maxQueuedFrames=" + actual.MaxQueuedFrames);
    }
}
