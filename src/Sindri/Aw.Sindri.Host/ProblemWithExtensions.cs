using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host;

/// <summary>
/// An error with extension members (RFC 9457 §3.2) as a ProblemDetails answer: each member is a top-level member after the
/// standard ones, and every string in it (values, and names inside objects) is made safe with <see cref="SafeText.Clean"/>
/// (scrubbed, escaped, one line, at most <see cref="MaxString"/> characters), for every caller alike. An error without
/// extensions never comes through here, so its answer is byte for byte what it always was.
/// </summary>
internal sealed class ProblemWithExtensions(Error error, int status) : IResult
{
    internal const int MaxString = 200;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var scrubber = httpContext.RequestServices.GetRequiredService<ITextScrubber>();
        var members = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in error.Extensions ?? ErrorExtensions.Empty)
        {
            members[name] = Clean(scrubber, value);
        }

        return TypedResults.Problem(title: error.Code, detail: error.Message, statusCode: status, extensions: members).ExecuteAsync(httpContext);
    }

    /// <summary>A copy of <paramref name="value"/> with every string leaf (and object member name) cleaned.</summary>
    internal static JsonElement Clean(ITextScrubber scrubber, JsonElement value)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, scrubber, value);
        }

        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, ITextScrubber scrubber, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            writer.WriteStringValue(SafeText.Clean(scrubber, value.GetString() ?? string.Empty, MaxString));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            WriteObject(writer, scrubber, value);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray())
            {
                Write(writer, scrubber, item);
            }

            writer.WriteEndArray();
        }
        else
        {
            value.WriteTo(writer);   // numbers, booleans and null carry no text
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, ITextScrubber scrubber, JsonElement value)
    {
        writer.WriteStartObject();
        foreach (var member in value.EnumerateObject())
        {
            writer.WritePropertyName(SafeText.Clean(scrubber, member.Name, MaxString));
            Write(writer, scrubber, member.Value);
        }

        writer.WriteEndObject();
    }
}
