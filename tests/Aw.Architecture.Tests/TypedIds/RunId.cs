using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Architecture.Tests.TypedIds;

// A copy of the example in tests/Aw.Kernel.Tests/TypedIds, as the guard tests' compliant case (that project can't be referenced).
/// <summary>A Guid id: one agent run.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(GuidIdJsonConverter<RunId>))]
internal readonly partial struct RunId : ITypedId<RunId, Guid>
{
    private static Validation Validate(Guid value) =>
        value == Guid.Empty ? Validation.Invalid("RunId must not be empty") : Validation.Ok;
}
