using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.read-tracker', on the block runtime (scoping spec §4, §8.2): reads the project or issue the scope input names through the tracker port (<see cref="ITrackerSource"/>:
/// at most 50 child issues and the first 20 comments of each), scrubs its text, asks the context engine's text shape for the anchors, then runs
/// <c language="text">ScopingSteps.ReadTracker</c>'s code. With no tracker configured the Step fails with SND-SCOPING-005 and the instance parks.
/// </summary>
/// <param name="tracker">The tracker port.</param>
/// <param name="shape">The engine's text shape.</param>
/// <param name="scrubber">Takes secrets out of tracker text.</param>
/// <param name="clock">When the record was read.</param>
internal sealed class ScopeReadTrackerStep(ITrackerSource tracker, IScopingTextShape shape, ITextScrubber scrubber, TimeProvider clock) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.read-tracker");

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var input = Fact(context, "scopeInput");
        if (!string.Equals(ScopeJson.Str(input, "kind"), "tracker", StringComparison.Ordinal) || ScopeJson.Str(input, "key") is not { Length: > 0 } key)
        {
            return SindriErrors.InvalidInput.With("read-tracker reads a tracker item: fact:scopeInput needs the kind 'tracker' and a key (fix: use the reader for that kind, or name the project or issue)");
        }

        var read = await tracker.Read(key, TrackerRead.Scoping, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error with { Message = $"{read.Error.Code}: {read.Error.Message}" };
        }

        var text = scrubber.Scrub(TrackerBrief.Compose(read.Value));
        var query = ContextQuery([]);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var asked = await shape.Anchors(context.Repo, text, cancellationToken).ConfigureAwait(false);
            if (asked.IsError)
            {
                return asked.Error;
            }

            query = ContextQuery(asked.Value);
        }

        var source = new JsonObject
        {
            ["key"] = read.Value.Key,
            ["title"] = scrubber.Scrub(read.Value.Title),
            ["text"] = text,
            ["version"] = read.Value.Version,
            ["fetchedAt"] = clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        };
        return Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["trackerSource"] = ScopeJson.Write(source), ["contextQuery"] = query });
    }
}
