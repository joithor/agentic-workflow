namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>What one round of a state has spent against its budget (the per-round rule).</summary>
/// <param name="Round">The loop round, from 1 (1 for a state with no loop).</param>
/// <param name="Used">Tokens the round's runs spent.</param>
/// <param name="Granted">Tokens '--grant-tokens' added to this round.</param>
public sealed record RoundSpend(int Round, int Used, int Granted);
