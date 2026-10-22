using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// The merge record's one writer (PB spec §4.7, BD7): appends a row to 'babysit.jsonl' in the instance's run area, never editing a line, and writes nothing when a row with the same identity is
/// already there, so a retried or resumed effect leaves one row. The blocks that own a row kind call <see cref="IMergeRecord.Append"/> with the fields of the kind.
/// </summary>
/// <param name="area">The instance's run area.</param>
/// <param name="clock">When a row is written.</param>
internal sealed class MergeRecord(IRunArea area, TimeProvider clock) : IMergeRecord
{
    /// <summary>The record's file name in the run area.</summary>
    public const string FileName = "babysit.jsonl";

    private static readonly HashSet<string> _kinds = new(StringComparer.Ordinal)
    {
        "start", "snapshot", "push", "comment", "recheck", "docs", "restack", "merge", "docsMerge", "end",
    };

    /// <inheritdoc/>
    public async ValueTask<Result<bool>> Append(MergeRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_kinds.Contains(row.Kind))
        {
            return WorkflowErrors.EffectFailed.With($"the merge record of {row.Instance} has no row kind '{row.Kind}' (PB spec §4.7)");
        }

        var before = await area.Read(row.Instance, FileName, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        if (Written(before.Value, row.Identity))
        {
            return false;
        }

        var line = new JsonObject
        {
            ["kind"] = row.Kind,
            ["identity"] = row.Identity,
            ["instance"] = row.Instance,
            ["headSha"] = row.HeadSha,
            ["at"] = clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var (name, value) in row.Fields)
        {
            if (!line.ContainsKey(name))
            {
                line[name] = value?.DeepClone();
            }
        }

        var written = await area.Write(row.Instance, FileName, line.ToJsonString() + "\n", append: true, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : true;
    }

    private static bool Written(string record, string identity)
    {
        var needle = "\"identity\":" + JsonValue.Create(identity).ToJsonString();
        return record.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line => line.Contains(needle, StringComparison.Ordinal));
    }
}
