namespace Aw.Sindri.Tests.Sessions;

/// <summary>One session as the discovery golden pins it: no host (CO-2b), no repo (no profile in the fixture).</summary>
internal sealed record GoldenSession(
    string Ref, string State, string Source, bool IdKnown, bool Estimated, int Pid, string? Title, string? Cwd, string? LaunchId, string? Team, string? Lane, string Since);
