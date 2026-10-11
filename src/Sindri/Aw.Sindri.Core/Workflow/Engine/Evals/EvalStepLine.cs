using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// A later line of an eval case (spec §9.2): '{ "trigger", "gates"?, "expect" }'. A gate the line doesn't name passes;
/// a gate it names answers with that verdict.
/// </summary>
/// <param name="Trigger">The trigger.</param>
/// <param name="Gates">The matched transition's gate verdicts, by gate.</param>
/// <param name="Expect">The expected state after it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalStepLine(EvalTrigger Trigger, IReadOnlyDictionary<BlockId, GateVerdict>? Gates, EvalExpectation Expect);
