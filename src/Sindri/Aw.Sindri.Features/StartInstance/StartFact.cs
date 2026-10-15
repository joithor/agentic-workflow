namespace Aw.Sindri.Features.StartInstance;

/// <summary>An input fact as the CLI sends it (engine spec §6.1): the file's name, which status shows, and its content (at most 1 MiB).</summary>
/// <param name="Name">The file name ('spec.md').</param>
/// <param name="Content">The file's content.</param>
public sealed record StartFact(string Name, ReadOnlyMemory<byte> Content);
