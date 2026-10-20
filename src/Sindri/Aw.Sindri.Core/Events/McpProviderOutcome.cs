namespace Aw.Sindri.Core.Events;

/// <summary>What registering Sindri's MCP server did for one provider (setup-once spec §7.5).</summary>
/// <param name="Provider">The provider ('claude', 'codex', 'cursor').</param>
/// <param name="Outcome">What happened ('registered', 'unchanged', 'unavailable', 'failed').</param>
public sealed record McpProviderOutcome(string Provider, string Outcome);
