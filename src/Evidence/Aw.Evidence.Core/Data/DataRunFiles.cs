using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Evidence.Core.Files;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>The data-evidence run's own files: the script it reads, the goldens it compares and the scrubbed summary it writes.</summary>
/// <param name="files">The run file port.</param>
/// <param name="scrubber">The pattern scrubber.</param>
/// <param name="json">The package's AwJson options.</param>
public sealed class DataRunFiles(IRunFiles files, ITextScrubber scrubber, JsonSerializerOptions json)
{
    /// <summary>The summary's file name inside the run directory.</summary>
    public const string SummaryFile = "summary.json";

    /// <summary>Reads a file.</summary>
    /// <param name="path">The file (absolute).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or the file port's error.</returns>
    public ValueTask<Result<byte[]>> ReadAsync(string path, CancellationToken cancellationToken) => files.ReadAsync(path, cancellationToken);

    /// <summary>Reads a database script, naming the file in a failure.</summary>
    /// <param name="path">The script's path, for the message.</param>
    /// <param name="bytes">The script's bytes.</param>
    /// <returns>The script, or AW-EVIDENCE-018.</returns>
    public Result<DbScript> ParseDb(string path, ReadOnlySpan<byte> bytes) =>
        DbScriptReader.Read(bytes, json).Match<Result<DbScript>>(script => script, error => error with { Message = $"script {path}: {error.Message}" });

    /// <summary>Reads an API script, naming the file in a failure.</summary>
    /// <param name="path">The script's path, for the message.</param>
    /// <param name="bytes">The script's bytes.</param>
    /// <returns>The script, or AW-EVIDENCE-017.</returns>
    public Result<ApiScript> Parse(string path, ReadOnlySpan<byte> bytes) =>
        ApiScriptReader.Read(bytes, json).Match<Result<ApiScript>>(script => script, error => error with { Message = $"script {path}: {error.Message}" });

    /// <summary>The scrubber for one run: the pattern scrubber, and each of <paramref name="secretValues"/> replaced by value first.</summary>
    /// <param name="secretValues">Values the run resolved that must never be recorded.</param>
    /// <returns>The scrubber.</returns>
    public ITextScrubber ScrubberFor(IEnumerable<string> secretValues) => new ValueScrubber(scrubber, secretValues);

    /// <summary>Makes the run directory and deletes an earlier summary in it, so a run that stops early never leaves a stale one.</summary>
    /// <param name="runDirectory">The run directory (absolute).</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or the file port's error.</returns>
    public async ValueTask<Result<Unit>> PrepareRunDirectoryAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var created = await files.CreateDirectoryAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created : await files.DeleteAsync(RunPath.Combine(runDirectory, SummaryFile), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a run summary as &lt;run-dir&gt;/summary.json through the scrubber (every string in the document), then reads it back.</summary>
    /// <param name="runDirectory">The run directory (absolute).</param>
    /// <param name="summary">The summary.</param>
    /// <param name="runScrubber">The run's scrubber.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <typeparam name="TSummary">The summary type, known to the package's JSON context.</typeparam>
    /// <returns>The summary as read back and the exact document written, or an error.</returns>
    public async ValueTask<Result<(TSummary Summary, string Document)>> WriteDocumentAsync<TSummary>(string runDirectory, TSummary summary, ITextScrubber runScrubber, CancellationToken cancellationToken)
        where TSummary : notnull
    {
        var node = JsonNode.Parse(AwJson.Serialize(summary, json)) ?? throw new InvalidOperationException("a summary serializes to a JSON object");
        var document = Scrub(node, runScrubber).ToJsonString(json);
        var written = await files.WriteAsync(RunPath.Combine(runDirectory, SummaryFile), Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        return AwJson.Deserialize<TSummary>(Encoding.UTF8.GetBytes(document), json).Map(read => (read, document));
    }

    private static JsonNode Scrub(JsonNode node, ITextScrubber runScrubber) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(entry => KeyValuePair.Create(entry.Key, entry.Value is null ? null : Scrub(entry.Value, runScrubber)))),
        JsonArray array => new JsonArray([.. array.Select(item => item is null ? null : Scrub(item, runScrubber))]),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(runScrubber.Scrub(text)),
        _ => node.DeepClone(),
    };
}
