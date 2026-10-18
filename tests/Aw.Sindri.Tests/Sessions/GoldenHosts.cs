namespace Aw.Sindri.Tests.Sessions;

/// <summary>The hosts of the fixture snapshot as Golden/snapshot-hosts.json pins them.</summary>
internal sealed record GoldenHosts(IReadOnlyList<GoldenHost> Hosts);
