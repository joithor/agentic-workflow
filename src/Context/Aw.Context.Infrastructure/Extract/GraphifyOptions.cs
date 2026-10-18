namespace Aw.Context.Infrastructure.Extract;

/// <summary>How graphify is found. Its limits (time, snapshot, file and graph size) come from the profile on each build, as GraphifyLimits.</summary>
/// <param name="Pin">The version this build accepts (sindri/package.json's graphifyPin).</param>
/// <param name="Binary">The graphify command name or absolute path.</param>
internal sealed record GraphifyOptions(string Pin, string Binary)
{
    /// <summary>The shipped options.</summary>
    public static GraphifyOptions Default { get; } = new("0.9.67", "graphify");

    /// <summary>Wraps a command in the sandbox (null: this machine has none). Tests swap it for a pass-through so the run path needs no bwrap.</summary>
    public Func<SandboxHost, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>?> Sandbox { get; init; } = GraphifySandbox.Argv;
}
