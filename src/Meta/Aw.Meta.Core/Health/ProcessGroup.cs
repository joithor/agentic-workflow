namespace Aw.Meta.Core.Health;

/// <summary>Processes of one kind and what they hold.</summary>
/// <param name="Name">The group: claude, dotnet, temporal, mcp, docker/qemu, onyx/opensearch/vespa, node or other.</param>
/// <param name="Count">How many processes.</param>
/// <param name="RssMb">Their resident memory in megabytes.</param>
public sealed record ProcessGroup(string Name, int Count, long RssMb);
