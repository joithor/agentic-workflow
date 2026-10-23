namespace Aw.Sindri.Core.EngineReads;

/// <summary>The kinds of work an instance waits on (API spec §3.8, E-30).</summary>
public static class EngineWaitKinds
{
    /// <summary>An activity is running or retrying.</summary>
    public const string Activity = "activity";

    /// <summary>The instance waits for a signal.</summary>
    public const string Signal = "signal";

    /// <summary>The instance waits for an update.</summary>
    public const string Update = "update";

    /// <summary>A child instance is running.</summary>
    public const string Child = "child";

    /// <summary>A timer is running.</summary>
    public const string Timer = "timer";
}
