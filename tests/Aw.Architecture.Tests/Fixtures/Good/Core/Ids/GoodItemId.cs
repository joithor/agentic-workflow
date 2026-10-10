using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Architecture.Tests.Fixtures.Good.Core.Ids;

[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<GoodItemId>))]
internal readonly partial struct GoodItemId : ITypedId<GoodItemId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("GoodItemId must not be blank") : Validation.Ok;
}
