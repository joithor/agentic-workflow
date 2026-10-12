using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>Ties a caller's command to every event it causes: the envelope's correlation (factory addendum §17.21).</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(GuidIdJsonConverter<CorrelationId>))]
public readonly partial struct CorrelationId : ITypedId<CorrelationId, Guid>
{
    private static Validation Validate(Guid value) =>
        value == Guid.Empty ? Validation.Invalid("CorrelationId must not be empty") : Validation.Ok;
}
