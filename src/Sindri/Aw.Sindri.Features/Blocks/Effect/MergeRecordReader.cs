using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>Reads 'babysit.jsonl' back for the babysitter stats: a missing file is an empty record, and a line that is not a JSON object with a kind counts as an error and is left out.</summary>
/// <param name="area">The run area the record lives in.</param>
internal sealed class MergeRecordReader(IRunArea area) : IMergeRecordReader
{
    public async ValueTask<Result<MergeRecordRows>> Read(string address, CancellationToken cancellationToken)
    {
        var text = await area.Read(address, MergeRecord.FileName, cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return text.Error;
        }

        var rows = new List<MergeRowRead>();
        var errors = 0;
        foreach (var line in text.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Parse(line) is { } row)
            {
                rows.Add(row);
            }
            else
            {
                errors++;
            }
        }

        return new MergeRecordRows(rows, errors);
    }

    private static MergeRowRead? Parse(string line)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject fields && fields["kind"] is JsonValue kind && kind.TryGetValue<string>(out var name) && name.Length > 0 ? new MergeRowRead(name, fields) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
