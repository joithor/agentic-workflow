using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'EvaluateGates' activity is given (engine P7 plan D7): the matched transition's gates, in order.</summary>
/// <param name="Address">The instance address.</param>
/// <param name="Gates">The transition's gates.</param>
/// <param name="Facts">The instance's facts as stored references; the activity reads the content a gate's argument names, so no fact content enters the workflow's history.</param>
/// <param name="TokensLeft">The tokens the item may still spend; null when no budget applies ('gate.budget.available' reads it).</param>
internal sealed record GateRun(string Address, IReadOnlyList<GateUse> Gates, IReadOnlyDictionary<FactKey, FactValue> Facts, int? TokensLeft);
