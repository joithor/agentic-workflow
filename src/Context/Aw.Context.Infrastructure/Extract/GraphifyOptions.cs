namespace Aw.Context.Infrastructure.Extract;

/// <summary>How graphify is found. Its limits (time, snapshot, file and graph size) come from the profile on each build, as ClusterLimits.</summary>
/// <param name="Pin">The version this build accepts (context/graphify.pin).</param>
/// <param name="Binary">The graphify command name or absolute path.</param>
internal sealed record GraphifyOptions(string Pin, string Binary)
{
    /// <summary>The shipped options.</summary>
    public static GraphifyOptions Default { get; } = new("0.9.67", "graphify");

    /// <summary>How long one configured minute lasts. Tests shorten it so a timeout needn't wait a real minute; it is never set from a profile.</summary>
    public TimeSpan MinuteLength { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Wraps a command in the sandbox (null: this machine has none). Tests swap it for a pass-through so the run path needs no bwrap.</summary>
    public Func<SandboxHost, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>?> Sandbox { get; init; } = GraphifySandbox.Argv;
}
