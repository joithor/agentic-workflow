using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>The run's own files: the script it reads, the goldens it compares and the scrubbed summary it writes.</summary>
internal sealed class ApiRunFiles(IRunFiles files, ITextScrubber scrubber, JsonSerializerOptions json)
{
    internal const string SummaryFile = "summary.json";

    internal ValueTask<Result<byte[]>> ReadAsync(string path, CancellationToken cancellationToken) => files.ReadAsync(path, cancellationToken);

    internal Result<ApiScript> Parse(string path, ReadOnlySpan<byte> bytes) =>
        ApiScriptReader.Read(bytes, json).Match<Result<ApiScript>>(script => script, error => error with { Message = $"script {path}: {error.Message}" });

    internal Collected NewCollected() => new(scrubber);

    internal string Safe(string text, int limit) => RecordedText.Of(scrubber, text, limit);

    // A run that stops early must not leave an earlier run's summary behind as if it were this run's evidence.
    internal async ValueTask<Result<Unit>> PrepareRunDirectoryAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var created = await files.CreateDirectoryAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created : await files.DeleteAsync(RunPath.Combine(runDirectory, SummaryFile), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the whole summary through the scrubber: every string in the document is scrubbed, then written and read back.</summary>
    internal async ValueTask<Result<ApiRunResponse>> WriteSummaryAsync(string runDirectory, ApiSummary summary, CancellationToken cancellationToken)
    {
        var node = JsonNode.Parse(AwJson.Serialize(summary, json)) ?? throw new InvalidOperationException("a summary serializes to a JSON object");
        var document = Scrub(node).ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = json.Encoder });
        var written = await files.WriteAsync(RunPath.Combine(runDirectory, SummaryFile), System.Text.Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        return AwJson.Deserialize<ApiSummary>(System.Text.Encoding.UTF8.GetBytes(document), json).Map(read => new ApiRunResponse(read, document));
    }

    private JsonNode Scrub(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(entry => KeyValuePair.Create(entry.Key, entry.Value is null ? null : Scrub(entry.Value)))),
        JsonArray array => new JsonArray([.. array.Select(item => item is null ? null : Scrub(item))]),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(scrubber.Scrub(text)),
        _ => node.DeepClone(),
    };
}
