using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Line;

/// <summary>The plan evidence's packet work: writes the plan packet and each phase packet into the run area, has aw-packets check them against the checkout, and names the result.</summary>
/// <param name="packets">aw-packets.</param>
/// <param name="area">The instance's run area.</param>
/// <param name="where">The instance's address, the repo's checkout and the repo slug ('owner/name').</param>
internal sealed class PlanSeal(IPackets packets, IRunArea area, (string Address, string Checkout, string Repo) where)
{
    private const string Step = "step.plan.evidence";

    /// <summary>Assembles, writes and checks the plan packet and the phase packets.</summary>
    /// <param name="scope">The 'fact:scope' object.</param>
    /// <param name="design">The 'fact:design' object.</param>
    /// <param name="plan">The 'fact:plan' tree.</param>
    /// <param name="sets">The holdout sets and the answer log.</param>
    /// <param name="cancellationToken">Cancels the writes and checks.</param>
    /// <returns>'complete' with the plan packet fact, or 'incomplete' with the findings; a failure when the tool or the run area cannot do its job.</returns>
    internal async ValueTask<BlockStepResult> Run(JsonNode scope, JsonNode design, JsonNode plan, (JsonNode? Holdout, JsonNode? AnswerLog) sets, CancellationToken cancellationToken)
    {
        var files = PlanPacket.Assemble(scope, design, plan, sets, where.Repo);
        if (files.IsError)
        {
            return LinePackets.Gaps("incomplete", [LinePackets.Finding("PLN-PKT-001", files.Error.Message, "Complete the plan so it can be a packet.")]);
        }

        var planned = await Check(PacketKind.Plan, LinePackets.PlanDirectory, files.Value, cancellationToken).ConfigureAwait(false);
        if (planned.Packet is not { } planPacket)
        {
            return planned.Stop ?? BlockStepResult.Failed(Step); // why: Check sets Stop whenever it returns no packet
        }

        var phases = new JsonArray();
        var graph = LineJson.Items(PlanGraph.Derive(plan), "phases").ToList();
        foreach (var phase in LineJson.Items(plan, "phases"))
        {
            var (stop, entry) = await Phase(new PhaseInput(scope, design, phase, planPacket.Hash, (graph, sets.Holdout)), cancellationToken).ConfigureAwait(false);
            if (stop is not null)
            {
                return stop;
            }

            LineJson.Append(phases, entry ?? []);
        }

        var fact = planPacket.ToJson();
        fact["phases"] = phases;
        var recommendation = new JsonObject { ["outcome"] = "approve", ["why"] = "Every design and plan check passed and each phase packet validates." };
        return StepFacts.Done("complete", ("fact:planPacket", fact), ("fact:recommendation", recommendation));
    }

    private async ValueTask<(BlockStepResult? Stop, JsonObject? Entry)> Phase(PhaseInput input, CancellationToken cancellationToken)
    {
        var id = PlanContext.Id(input.Phase);
        var set = LineJson.Items(input.Sets.Holdout).FirstOrDefault(s => string.Equals(LineJson.Str(s, "phase"), id, StringComparison.Ordinal));
        var entry = input.Sets.Graph.Find(g => string.Equals(LineJson.Str(g, "id"), id, StringComparison.Ordinal));
        var made = PhasePackets.Assemble(input.Scope, input.Design, input.Phase, (input.PlanHash, entry, set, where.Repo));
        if (made.IsError)
        {
            return (LinePackets.Gaps("incomplete", [LinePackets.Finding("PLN-PKT-001", made.Error.Message, "Complete the phase so it can be a packet.")]), null);
        }

        var checkedPhase = await Check(PacketKind.Phase, PhasePackets.Directory(id), made.Value, cancellationToken).ConfigureAwait(false);
        return checkedPhase.Packet is { } packet
            ? (null, new JsonObject { ["phase"] = id, ["id"] = PhasePackets.Id(input.Scope, id), ["dir"] = packet.Directory, ["hash"] = packet.Hash })
            : (checkedPhase.Stop, null);
    }

    private async ValueTask<Checked> Check(PacketKind kind, string directory, IReadOnlyList<PacketText> files, CancellationToken cancellationToken)
    {
        var name = kind.ToString().ToLowerInvariant();
        var written = await LinePackets.Write(area, where.Address, directory, files, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return Checked.Broke($"{Step}: can't write the {name} packet: {written.Error.Message}");
        }

        var packet = new PacketRef(kind, written.Value, where.Checkout);
        var validation = await packets.Validate(packet, cancellationToken).ConfigureAwait(false);
        if (validation.IsError)
        {
            return Checked.Broke($"{Step}: aw-packets could not check the {name} packet: {validation.Error.Message}");
        }

        if (!validation.Value.Passed)
        {
            return new Checked(Packet: null, LinePackets.Gaps("incomplete", LinePackets.Findings(validation.Value.Failures)));
        }

        var hash = await packets.Hash(packet, cancellationToken).ConfigureAwait(false);
        return hash.IsError ? Checked.Broke($"{Step}: aw-packets could not hash the {name} packet: {hash.Error.Message}") : new Checked(new PacketFact(hash.Value, written.Value), Stop: null);
    }

    private sealed record PhaseInput(JsonNode Scope, JsonNode Design, JsonNode Phase, string PlanHash, (List<JsonNode> Graph, JsonNode? Holdout) Sets);

    private sealed record Checked(PacketFact? Packet, BlockStepResult? Stop)
    {
        internal static Checked Broke(string failure) => new(Packet: null, BlockStepResult.Failed(failure));
    }
}
