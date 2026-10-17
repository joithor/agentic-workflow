namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One terminals.json row.</summary>
internal sealed record TerminalRow(string Id, IReadOnlyList<string>? ProcessNames);
