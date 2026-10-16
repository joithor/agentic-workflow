using System.Text.Json;
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

    /// <summary>The scrubber for one run: the pattern scrubber, and each header value and passed-through value replaced by value first.</summary>
    internal ITextScrubber ScrubberFor(IEnumerable<string> headerValues, IEnumerable<string> envValues) => new ValueScrubber(scrubber, headerValues, envValues);

    // A run that stops early must not leave an earlier run's summary behind as if it were this run's evidence.
    internal async ValueTask<Result<Unit>> PrepareRunDirectoryAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var created = await files.CreateDirectoryAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created : await files.DeleteAsync(RunPath.Combine(runDirectory, SummaryFile), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the summary, then reads it back. The free-text fields (headline, step output, failures) were scrubbed as they were recorded;
    /// the refusal and error messages are scrubbed here. Fixed fields (host, outcome, names, ids) are never value-scrubbed: a secret that happens to equal one would corrupt the file.
    /// </summary>
    internal async ValueTask<Result<ApiRunResponse>> WriteSummaryAsync(string runDirectory, ApiSummary summary, ITextScrubber runScrubber, CancellationToken cancellationToken)
    {
        var scrubbed = summary with { Refusal = Messaged(summary.Refusal, runScrubber), Error = Messaged(summary.Error, runScrubber) };
        var document = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(scrubbed, json));
        var written = await files.WriteAsync(RunPath.Combine(runDirectory, SummaryFile), System.Text.Encoding.UTF8.GetBytes(document), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        return AwJson.Deserialize<ApiSummary>(System.Text.Encoding.UTF8.GetBytes(document), json).Map(read => new ApiRunResponse(read, document));
    }

    private static CodedMessage? Messaged(CodedMessage? message, ITextScrubber runScrubber) => message is null ? null : message with { Message = runScrubber.Scrub(message.Message) };
}
