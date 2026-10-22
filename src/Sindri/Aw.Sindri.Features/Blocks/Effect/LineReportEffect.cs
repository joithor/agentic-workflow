using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.line.report' (line spec BD7, LN-WF1, Q13): the phase tells its line that it has 'shipped' or 'merged'. A phase that belongs to a line
/// (its 'line' fact names one) appends a row to 'line-report.jsonl' in the instance's run area, one per effect identity, and replies nothing; a phase
/// outside a line records nothing. The delivery of 'line.phaseChanged' to the linked parent belongs to the line's dispatcher (LN-WF1), which reads
/// this record; it is not this effect's.
/// </summary>
/// <param name="area">The instance's run area, where the report is recorded.</param>
/// <param name="contents">Reads the line context fact.</param>
/// <param name="store">Stores the 'fact:phaseEvent' the reply carries.</param>
/// <param name="clock">When a report is written.</param>
internal sealed class LineReportEffect(IRunArea area, IFactContents contents, IFactStore store, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    private const string Record = "line-report.jsonl";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => LineEffects.Report;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (Status(context) is not { } status)
        {
            return Failed(context, "status 'shipped' or 'merged'");
        }

        var line = await ReadLine(context, cancellationToken).ConfigureAwait(false);
        if (line.IsError)
        {
            return line.Error;
        }

        return Name(line.Value, "line") is { } name
            ? await Append(context, status, name, Name(line.Value, "phase"), cancellationToken).ConfigureAwait(false)
            : new EffectDone(Reply: null, Existed: false);
    }

    private static string? Status(EffectContext context) =>
        context.Arguments.TryGetValue("status", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is "shipped" or "merged" ? value.GetString() : null;

    private static string? Name(JsonElement line, string property) =>
        line.ValueKind == JsonValueKind.Object && line.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static Error Failed(EffectContext context, string what) =>
        WorkflowErrors.EffectFailed.With($"{context.Effect.Value} of {context.Instance} needs {what} (fix: pass it in the transition's with:)");

    private async ValueTask<Result<EffectDone>> Append(EffectContext context, string status, string line, string? phase, CancellationToken cancellationToken)
    {
        var before = await area.Read(context.Instance, Record, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        var identity = JsonValue.Create(context.Identity).ToJsonString();
        if (before.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(row => row.Contains("\"identity\":" + identity, StringComparison.Ordinal)))
        {
            return new EffectDone(Reply: null, Existed: true);
        }

        var row = new JsonObject
        {
            ["status"] = status,
            ["identity"] = context.Identity,
            ["instance"] = context.Instance,
            ["line"] = line,
            ["phase"] = phase,
            ["at"] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
        var written = await area.Write(context.Instance, Record, row.ToJsonString() + "\n", append: true, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new EffectDone(Reply: null, Existed: false);
    }

    private async ValueTask<Result<JsonElement>> ReadLine(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue("line", out var stored) || stored.ValueKind != JsonValueKind.Object
            || !stored.TryGetProperty("ref", out var reference) || reference.ValueKind != JsonValueKind.String
            || !stored.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String)
        {
            return Failed(context, "'line' as a stored fact (the line context)");
        }

        var read = await contents.Read(new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null), cancellationToken).ConfigureAwait(false);   // why: both checked to be strings above
        if (read.IsError)
        {
            return read.Error;
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value);
            return document.RootElement.Clone();
        }
        catch (JsonException)   // boundary: a fact that isn't JSON names no line
        {
            return Failed(context, "'line' as JSON");
        }
    }
}
