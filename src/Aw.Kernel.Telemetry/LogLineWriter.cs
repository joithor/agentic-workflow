using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aw.Kernel.Events;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// Writes one log entry as a JSON line (no reflection): timestamp, level, category, eventId, eventName, message,
/// exception, traceId, spanId, the event context's actor, tenant, ring and correlation, and the structured fields.
/// Every text value goes through the scrubber first.
/// </summary>
internal static class LogLineWriter
{
    private static readonly JsonWriterOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Write(LogEntry entry, DateTimeOffset now, Scrubber scrubber, ref ScrubTally tally)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var json = new Utf8JsonWriter(buffer, _options))
        {
            json.WriteStartObject();
            WriteHeader(json, entry, now, scrubber, ref tally);
            WriteSpan(json, entry.Span);
            if (entry.Context is { } context)
            {
                WriteContext(json, context, scrubber, ref tally);
            }

            WriteFields(json, entry.Fields, scrubber, ref tally);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteHeader(Utf8JsonWriter json, LogEntry entry, DateTimeOffset now, Scrubber scrubber, ref ScrubTally tally)
    {
        json.WriteString("timestamp", now.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        json.WriteString("level", LevelName(entry.Level));
        json.WriteString("category", scrubber.Scrub(entry.Category, ref tally));
        json.WriteNumber("eventId", entry.EventId.Id);
        if (entry.EventId.Name is { } eventName)
        {
            json.WriteString("eventName", scrubber.Scrub(eventName, ref tally));
        }

        json.WriteString("message", scrubber.Scrub(entry.Message, ref tally));
        if (entry.Exception is { } exception)
        {
            json.WriteString("exception", scrubber.Scrub(exception.ToString(), ref tally));
        }
    }

    private static void WriteSpan(Utf8JsonWriter json, Activity? span)
    {
        if (span is null)
        {
            json.WriteNull("traceId");
            json.WriteNull("spanId");
            return;
        }

        json.WriteString("traceId", span.TraceId.ToHexString());
        json.WriteString("spanId", span.SpanId.ToHexString());
    }

    private static void WriteContext(Utf8JsonWriter json, EventContext context, Scrubber scrubber, ref ScrubTally tally)
    {
        json.WriteString("actor", scrubber.Scrub(context.Actor, ref tally));
        json.WriteString("tenant", scrubber.Scrub(context.Tenant, ref tally));
        json.WriteNumber("ring", context.Ring);
        json.WriteString("correlationId", context.CorrelationId);
        if (context.CausationId is { } causation)
        {
            json.WriteString("causationId", causation);
        }
    }

    private static void WriteFields(Utf8JsonWriter json, IReadOnlyList<KeyValuePair<string, object?>> fields, Scrubber scrubber, ref ScrubTally tally)
    {
        var any = false;
        foreach (var (key, value) in fields)
        {
            if (key == MessageText.TemplateKey)
            {
                continue;
            }

            if (!any)
            {
                json.WriteStartObject("fields");
                any = true;
            }

            json.WriteString(key, scrubber.ScrubField(key, value, ref tally));
        }

        if (any)
        {
            json.WriteEndObject();
        }
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "trace",
        LogLevel.Debug => "debug",
        LogLevel.Information => "information",
        LogLevel.Warning => "warning",
        LogLevel.Error => "error",
        LogLevel.Critical => "critical",
        LogLevel.None => "none",
        _ => "none",
    };
}
