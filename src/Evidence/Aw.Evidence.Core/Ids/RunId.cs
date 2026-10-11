using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Evidence.Core.Ids;

/// <summary>RunId: the id of a run (the run directory's name).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RunId>))]
public readonly partial struct RunId : ITypedId<RunId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("RunId must not be blank") : Validation.Ok;
}
