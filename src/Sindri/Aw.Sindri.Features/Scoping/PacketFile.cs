namespace Aw.Sindri.Features.Scoping;

/// <summary>One packet file as served: JSON files as JSON, markdown and JSON lines as text.</summary>
/// <param name="Name">The file's name.</param>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Format">'json', 'jsonl' or 'md'.</param>
public sealed record PacketFile(string Name, string Text, string Format);
