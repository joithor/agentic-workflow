namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>config/launcher/terminals.json as host detection reads it: each row's id and process names; the launch argv and every other member are ignored.</summary>
internal sealed record TerminalsDocument(IReadOnlyList<TerminalRow> Terminals);
