using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Meta.Core.Ids;

/// <summary>MessageId: the id of a Meta message.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<MessageId>))]
public readonly partial struct MessageId : ITypedId<MessageId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("MessageId must not be blank") : Validation.Ok;
}
