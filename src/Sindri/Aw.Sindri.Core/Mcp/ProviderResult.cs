namespace Aw.Sindri.Core.Mcp;

/// <summary>One provider's line of the registration.</summary>
/// <param name="Provider">Which provider.</param>
/// <param name="Outcome">What happened.</param>
/// <param name="Detail">The file or command, or why it was skipped or failed.</param>
/// <param name="Fix">What the human does about a failure or a trust prompt; null when nothing.</param>
public sealed record ProviderResult(ProviderFile Provider, ProviderOutcome Outcome, string Detail, string? Fix);
