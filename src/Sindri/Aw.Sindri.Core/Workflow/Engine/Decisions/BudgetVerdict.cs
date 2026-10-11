using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>A run's spend against the state's and the recipe's budget (spec §3.7): the new spend, and a budget park when one ran out.</summary>
/// <param name="Spent">The spend after the run.</param>
/// <param name="Park">The budget park (kind step, resume rerun); null while within budget.</param>
public sealed record BudgetVerdict(TokenSpend Spent, SuspendOutcome? Park);
