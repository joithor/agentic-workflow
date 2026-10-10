using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Redaction;

namespace Aw.Judge.Infrastructure.Redaction;

/// <summary>
/// IInputRedactor over Aw.Redaction: every string value and key goes through the shared scrubber, then through the
/// judge's own patterns (the TS redactSecrets set), which keep the standalone 40+ hex rule Aw.Redaction leaves out.
/// </summary>
/// <param name="redactor">The shared scrubber.</param>
internal sealed class AwInputRedactor(IRedactor redactor) : IInputRedactor
{
    /// <summary>What a judge pattern's match becomes.</summary>
    public const string Placeholder = "[REDACTED]";

    /// <inheritdoc />
    public JsonNode? Redact(JsonNode? input) =>
        throw new NotSupportedException($"red: redact with {redactor.GetType().Name}");
}
