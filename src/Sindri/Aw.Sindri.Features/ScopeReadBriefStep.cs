using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.scope.read-brief', on the block runtime (scoping spec §7, §8.2): reads the brief file the scope input names from the item's repo checkout
/// (<see cref="IBriefFiles"/>: inside the repo, never a secret, never through a link that leaves it), asks the context engine's text shape for its anchors, then runs
/// <see cref="ScopingSteps.ReadBrief"/>'s code. An item with no repo in the approved profile has no checkout to read from, so the Step fails and the instance parks.
/// </summary>
/// <param name="files">Reads the brief.</param>
/// <param name="approvals">The approved profile, for the repo's checkout.</param>
/// <param name="shape">The engine's text shape.</param>
/// <param name="clock">When the file was read.</param>
internal sealed class ScopeReadBriefStep(IBriefFiles files, IApprovedProfile approvals, IScopingTextShape shape, TimeProvider clock) : ScopingCodeStep(Descriptor), IDescribedBlock
{
    private const string NoAnchors = """{"suggestedAnchors":[]}""";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.ReadBrief;

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var input = Fact(context, "scopeInput");
        if (!string.Equals(ScopeJson.Str(input, "kind"), "brief", StringComparison.Ordinal) || BriefSource.PathOf(input) is not { } path)
        {
            return SindriErrors.InvalidInput.With("read-brief reads a brief: fact:scopeInput needs the kind 'brief' and a path (fix: use the reader for that kind, or name the file)");
        }

        var root = await Checkout(context, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var file = await files.Read(path, [root.Value], cancellationToken).ConfigureAwait(false);
        if (file.IsError)
        {
            return file.Error;
        }

        var body = DeskBlocks.BriefBody(input, file.Value.Text);
        var anchors = string.IsNullOrWhiteSpace(body) ? NoAnchors : null;
        if (anchors is null)
        {
            var asked = await shape.Anchors(context.Repo, body, cancellationToken).ConfigureAwait(false);
            if (asked.IsError)
            {
                return asked.Error;
            }

            anchors = asked.Value;
        }

        return Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["briefSource"] = BriefSource.Json(file.Value, clock.GetUtcNow()), ["contextQuery"] = anchors });
    }

    // The item's repo checkout from the approved profile: the only root a brief may be read from here (the notes dir is a desk option).
    private async ValueTask<Result<string>> Checkout(StepContext context, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var repo = context.Repo is { } name ? approved.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, name.Value, StringComparison.Ordinal)) : null;
        return repo is null
            ? SindriErrors.InvalidInput.With("the item has no repo in the approved profile, so there is no checkout to read the brief from (fix: add the repo to the profile and approve it)")
            : repo.Path;
    }
}
