using System.Text.Json.Nodes;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.scope.propose', on the block runtime (scoping spec §6.1, C9): submits the map's surfaces that have no node key as draft proposals, the run being the
/// instance's address (a resubmission replaces the run's proposals, an empty list withdraws them), and records each surface's graph status through
/// <see cref="ScopingSteps.Propose"/>'s code. With no repo for the item, or the context engine unable to answer, nothing is submitted and the Step reports
/// 'skipped' (statuses 'unknown'), which the stop shows.
/// </summary>
/// <param name="proposals">The run's proposals on the context map.</param>
internal sealed class ScopeProposeStep(IScopeRunProposals proposals) : ScopingCodeStep(Descriptor), IDescribedBlock
{
    private static readonly HashSet<string> _kinds = new(["feature", "surface", "action", "thing", "flow"], StringComparer.Ordinal);

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Propose;

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Repo is not { } repo || context.Address is not { } address)
        {
            return Facts(facts);
        }

        var surfaces = ScopeJson.Items(Fact(context, "scopeMap"), "surfaces").Where(static s => ScopeJson.Str(s, "key") is null).Select(Surface).OfType<ProposedSurface>().ToList();
        var proposed = await proposals.Replace(address, repo, surfaces, cancellationToken).ConfigureAwait(false);
        if (!proposed.IsError)
        {
            facts["proposeResult"] = Answer(proposed.Value);
        }

        return Facts(facts);
    }

    private static ProposedSurface? Surface(JsonNode surface) =>
        ScopeJson.Str(surface, "title") is { } title
            ? new ProposedSurface(title, Key: null, ScopeJson.Str(surface, "kind") is { } kind && _kinds.Contains(kind) && Enum.TryParse<NodeKind>(kind, ignoreCase: true, out var parsed) ? parsed : null)
            : null;

    // The answer 'context map propose --json' prints, which the desk code reads: each surface with its node key and status.
    private static string Answer(ProposeContextMapResponse response) => ScopeJson.Write(new JsonObject
    {
        ["surfaces"] = new JsonArray([.. response.Surfaces.Select(static s => (JsonNode?)new JsonObject
        {
            ["surface"] = s.Surface,
            ["node"] = s.Node?.Value,
            ["status"] = s.Status switch { GraphStatusKind.Mapped => "mapped", GraphStatusKind.New => "new", GraphStatusKind.Unmapped => "unmapped", _ => "unmapped" },
        })]),
    });
}
