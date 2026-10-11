using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Evidence.Core.Ids;

/// <summary>TestId: the id of an element's data-testid.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TestId>))]
public readonly partial struct TestId : ITypedId<TestId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("TestId must not be blank") : Validation.Ok;
}
