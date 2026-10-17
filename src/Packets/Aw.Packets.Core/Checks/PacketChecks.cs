using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>Runs every packet rule in <see cref="PacketRules"/> order. Pure: the base commit's files come in.</summary>
internal static class PacketChecks
{
    internal static IReadOnlyList<CheckResult> Run(Packet packet, BaseTree tree, SizesFile? sizes)
    {
        var checks = new CheckList();
        StructureChecks.Run(packet, checks);
        ScopeChecks.Run(packet, tree, checks);
        ContentChecks.Run(packet, checks);
        SizeChecks.Run(packet, tree, sizes, checks);
        checks.Unchecked(PacketRules.WorkflowCoverage, "workflow coverage (addendum §17.7) needs the lens catalog and workflow generation, which don't exist yet");
        return checks.Results;
    }

    // The plan graph's task plan (line spec LN10) gets the build kind's task rules; the size budget stays unchecked
    // (LN-PKT3), and workflow coverage belongs to a build's workflow, so a plan graph doesn't list it.
    internal static IReadOnlyList<CheckResult> RunPlan(Packet packet, BaseTree tree, SizesFile? sizes)
    {
        var checks = new CheckList();
        StructureChecks.Run(packet, checks);
        ScopeChecks.Run(packet, tree, checks);
        ContentChecks.Run(packet, checks);
        SizeChecks.Run(packet, tree, sizes, checks);
        return checks.Results;
    }
}
