using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// SHIP-SECRET-001's scan, in process, over the lines a rebase put on top of the new base (PB spec §4.6, X1): the same patterns as 'gate.pr.secret-scan' (the shared scrubber over Aw.Redaction). A hit
/// names file and line, never the match.
/// </summary>
/// <param name="content">Reads the lines the workspace's commits added since the new base.</param>
/// <param name="scrubber">The shared secret patterns.</param>
internal sealed class RebasedRangeScan(IDiffContent content, ITextScrubber scrubber)
{
    /// <summary>The lines since <paramref name="since"/> that hold a secret.</summary>
    /// <param name="worktree">The workspace, on the rebased head.</param>
    /// <param name="since">The commit the rebase went onto.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One 'path:line' per hit; an error when the diff can't be read, because an unscanned push must not pass.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> Hits(string worktree, string since, CancellationToken cancellationToken)
    {
        var added = await content.AddedLines(worktree, since, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return added.Error;
        }

        IReadOnlyList<string> hits = [.. SecretScan.Findings("SHIP-SECRET-001", [.. added.Value.Select(static l => new AddedLine(l.Path, l.Line, l.Text))], scrubber)
            .Select(static f => string.Create(CultureInfo.InvariantCulture, $"{f.Path}:{f.Line}"))];
        return Results.Success(hits);
    }
}
