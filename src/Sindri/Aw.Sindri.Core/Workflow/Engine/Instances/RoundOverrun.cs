namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A round that spent past its allowance, for status ("round 1 used 139k of 100k").</summary>
/// <param name="State">The state.</param>
/// <param name="Round">The round.</param>
/// <param name="Used">Tokens it spent.</param>
/// <param name="Allowed">Its allowance when it reported: the state's budget plus the round's grants.</param>
public sealed record RoundOverrun(string State, int Round, int Used, int Allowed);
