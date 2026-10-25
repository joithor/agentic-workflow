using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The plan station's checks (line spec §6.3): one pure function over the facts, run by 'step.plan.check' (draft) and 'step.plan.evidence'. The evidence
/// stage runs every rule. PLN-PKT-001 is aw-packets validate and PKT-SIZE-001's file and item limits are aw-packets' too: the block runtime has no
/// port to run it, so a plan gets a warning that they were not checked here, never a pass.
/// </summary>
internal static partial class PlanChecks
{
    /// <summary>Runs the rules of the stage.</summary>
    /// <param name="design">The 'fact:design' tree.</param>
    /// <param name="plan">The 'fact:plan' tree.</param>
    /// <param name="holdoutSets">The 'fact:holdoutSets' tree (evidence stage).</param>
    /// <param name="evidence">True for the evidence stage.</param>
    /// <returns>The report.</returns>
    internal static LineReport Run(JsonNode? design, JsonNode? plan, JsonNode? holdoutSets, bool evidence)
    {
        var c = new PlanContext(design, plan, holdoutSets);
        IEnumerable<LineFinding> findings = [.. PacketNote(c), .. Trace(c), .. Sizes(c), .. Signals(c), .. Stacks(c), .. TestsNamed(c)];
        if (evidence)
        {
            findings = [.. findings, .. Executable(c), .. Holdouts(c)];
        }

        return new LineReport(evidence, [.. findings]);
    }

    private static IEnumerable<LineFinding> PacketNote(PlanContext c)
    {
        if (c.Phases.Count > 0)
        {
            yield return DesignContext.Warning("PLN-PKT-001", item: null, "aw-packets validate was not run on the plan packet on this runtime (PLN-PKT-001, PKT-SIZE-001's limits)", "Run aw-packets validate at the desk.");
        }
    }
}
