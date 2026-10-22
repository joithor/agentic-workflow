using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>What 'step.plan.holdout' is told (line spec §9, LN-WF4): write each phase's hidden scenarios to a file in the run folder, so Sindri's 'step.plan.holdout-store' puts them in the private store. The scenarios never go into a fact, a packet or the repo.</summary>
internal sealed class HoldoutAuthorInstructions : IAgentStepInstructions
{
    private static readonly string[] _lines =
    [
        "Goal: for every phase of fact:plan, write the hidden scenarios its builder never sees: checks of the phase's visible acceptance items that a builder who only knew the items could still get wrong.",
        "Write one file, $AW_SINDRI_RUN_DIR/holdout-sets.json, as { \"sets\": [ { \"phase\": \"<plan phase id>\", \"scenarios\": [ { \"id\": \"<phase>-H1\", \"item\": \"<a visible acceptance item id of that phase>\", \"kind\": \"api|ui|db\", \"steps\": [\"...\"], \"expected\": \"...\" } ] } ] }.",
        "Write at least the plan's holdoutMin scenarios (default 2) for every phase, each naming one visible item and a kind. Do not write a canary: Sindri adds it.",
        "Read the design packet, not the planner's session. Never put a scenario's steps or expected text in a fact, a packet, a commit, a comment or the repo.",
        "Report written with fact:holdoutDraft, a file holding { \"dir\": \"<the value of $AW_SINDRI_RUN_DIR>\" } and nothing else.",
    ];

    private static readonly BlockId _step = BlockId.From("step.plan.holdout");

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;
}
