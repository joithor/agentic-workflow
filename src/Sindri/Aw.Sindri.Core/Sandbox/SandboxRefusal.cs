using System.Buffers;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Makes the error of a sandbox launch refusal: the catalogue code, the owner-facing detail, and the 'reason' (and 'source') extensions the engine and the app read.</summary>
public static class SandboxRefusal
{
    /// <summary>Makes the refusal error.</summary>
    /// <param name="definition">One of the SND-LAUNCH-014..021 entries.</param>
    /// <param name="reason">A reason from <see cref="SandboxReasons"/> (for -018 'http-' and a status).</param>
    /// <param name="detail">What happened this time (the failing argv, the missing tool), already scrubbed; or null.</param>
    /// <param name="source">The credential source for -017, or null.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pair isn't a defined (code, reason): a programming error.</exception>
    public static Error For(ErrorDefinition definition, string reason, string? detail = null, ModelLoginSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reason);
        if (!SandboxReasons.IsDefined(definition.Code, reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, $"{definition.Code} has no reason '{reason}'.");
        }

        var error = definition.With(detail is null ? $"{definition.Title} ({reason})" : $"{definition.Title} ({reason}): {detail}")
            .WithExtension("reason", Text(reason));
        return source is { } s ? error.WithExtension("source", Text(SandboxWire.Name(s))) : error;
    }

    private static JsonElement Text(string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
