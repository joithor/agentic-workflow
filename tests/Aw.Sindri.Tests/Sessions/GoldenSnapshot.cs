namespace Aw.Sindri.Tests.Sessions;

/// <summary>The snapshot as Golden/snapshot-discovery.json pins it.</summary>
internal sealed record GoldenSnapshot(IReadOnlyList<GoldenSession> Sessions, IReadOnlyList<GoldenSession> RecentlyEnded, IReadOnlyList<GoldenProblem> Problems);
