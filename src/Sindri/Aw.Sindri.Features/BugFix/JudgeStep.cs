using System.Text.Json.Nodes;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>
/// 'step.bugfix.judge' on the block runtime (DT spec 6.1): the resolution check of one candidate, which bugfix-state asks of judge once. The deterministic part is Sindri's: no evidence in 'fact:evidenceSummary'
/// is 'needsHuman' and judge is never asked, and a candidate whose check run did not pass (bugfix-state's refusal of that kind) is 'unresolved', never 'resolved'. Otherwise judge's decision is the outcome
/// ('escalated' is 'needsHuman'). 'fact:verdict' on every outcome: {"candidate":"c1","decision":"resolved","reasonCode":…,"reasons":[…],"asked":true}. Any other refusal, such as a second ask, fails the Step.
/// </summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class JudgeStep(IBugFixState state, IRunArea area) : IBlockStep, IBlockStepId
{
    private const string Name = "step.bugfix.judge";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (BugFixFacts.Candidate(StepFacts.Read(context, "fact:candidate")) is not ({ } id, _))
        {
            return BlockStepResult.Failed($"{Name} needs fact:candidate to carry the candidate's id");
        }

        if (!HasEvidence(StepFacts.Read(context, "fact:evidenceSummary")))
        {
            return Verdict("needsHuman", id, "no-evidence", ["no evidence summary: a person has to look"], asked: false);
        }

        var (directory, failure) = BugFixFacts.StateDirectory(Name, context, area);
        if (failure is not null)
        {
            return failure;
        }

        var judged = await state.Judge(directory, id, cancellationToken).ConfigureAwait(false);
        return judged switch
        {
            { Status: BugFixAnswerStatus.Ok, Value: { } verdict } => Verdict(Outcome(verdict.Decision), verdict.Candidate, verdict.ReasonCode, verdict.Reasons, asked: true),
            { Refusal.Kind: RefusalKind.NoPassingRun } => Verdict("unresolved", id, "check-not-passed", [$"the check did not pass on {id}: it cannot be judged resolved"], asked: false),
            _ => BugFixFacts.Failure(Name, judged),
        };
    }

    private static bool HasEvidence(JsonNode? evidence) => evidence switch
    {
        null => false,
        JsonObject obj => obj.Count > 0,
        JsonArray items => items.Count > 0,
        JsonValue value => !value.TryGetValue<string>(out var text) || text.Length > 0,
        _ => true,
    };

    private static string Outcome(JudgeDecision decision) => decision switch
    {
        JudgeDecision.Resolved => "resolved",
        JudgeDecision.Partial => "partial",
        JudgeDecision.Unresolved => "unresolved",
        JudgeDecision.Escalated => "needsHuman",
        _ => "needsHuman",
    };

    private static BlockStepResult Verdict(string outcome, string candidate, string reasonCode, IReadOnlyList<string> reasons, bool asked) =>
        StepFacts.Done(outcome, ("fact:verdict", new JsonObject
        {
            ["candidate"] = candidate,
            ["decision"] = outcome,
            ["reasonCode"] = reasonCode,
            ["reasons"] = new JsonArray([.. reasons.Select(static r => (JsonNode?)JsonValue.Create(r))]),
            ["asked"] = asked,
        }));
}
