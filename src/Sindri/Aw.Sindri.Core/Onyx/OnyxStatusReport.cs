namespace Aw.Sindri.Core.Onyx;

/// <summary>Where Onyx stands, as <see cref="OnyxStatusReader"/> reads it.</summary>
/// <param name="State">not-set-up until `sindri onyx up` finished its start step and the files are still there; then unknown when Docker isn't usable here, else running when nginx and the API server run, otherwise stopped.</param>
/// <param name="PinnedVersion">The pinned Onyx release.</param>
/// <param name="PinnedCommit">The commit its tag points to.</param>
/// <param name="Checks">docker, compose, memory, disk, port, in that order.</param>
/// <param name="Port">The port Onyx is on while running, otherwise the one setup would use.</param>
/// <param name="ExitCode">0 when every check is ok, otherwise 1.</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt; while running, otherwise null.</param>
/// <param name="RunningVersion">The release the running Onyx reports, or unreachable when it doesn't answer; null when it isn't running.</param>
public sealed record OnyxStatusReport(
    OnyxState State, string PinnedVersion, string PinnedCommit, IReadOnlyList<PreflightCheck> Checks, int Port, int ExitCode, string? Url, string? RunningVersion);
