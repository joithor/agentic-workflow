using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Ids;

/// <summary>QuestionId: the id of a question in a packet's decision log.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<QuestionId>))]
public readonly partial struct QuestionId : ITypedId<QuestionId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("QuestionId must not be blank") : Validation.Ok;
}
