using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// An outbox command's id (spec §5.4): a UUIDv8 (RFC 9562) built from SHA-256 over '{trigger key}:{effect index}', so
/// each Commit gets ids of its own and a replayed decision gets the same ones. Written as the lower-case 36-character
/// UUID text. 'workflow_commands' maps it back to its instance, the entry of the frame the transition enters, and its
/// origin.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<OutboxCommandId>))]
public readonly partial struct OutboxCommandId : ITypedId<OutboxCommandId, string>
{
    /// <summary>The id of <paramref name="value"/>.</summary>
    /// <param name="value">The UUIDv8.</param>
    /// <returns>The id.</returns>
    public static OutboxCommandId For(Guid value) => From(value.ToString("D"));

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An outbox command id is a version-8 UUID in lower-case text");

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-8[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
