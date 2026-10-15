using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Meta.Core.Ids;

/// <summary>SessionId: the id of a Claude session.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SessionId>))]
public readonly partial struct SessionId : ITypedId<SessionId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("SessionId must not be blank") : Validation.Ok;
}
