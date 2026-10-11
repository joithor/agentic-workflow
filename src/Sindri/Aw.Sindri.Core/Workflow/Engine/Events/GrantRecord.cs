namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>A grant an answer made (spec §5.2, W7).</summary>
/// <param name="Rounds">The rounds '--grant n' added.</param>
/// <param name="Tokens">The tokens '--grant-tokens n' added.</param>
public sealed record GrantRecord(int? Rounds, int? Tokens);
