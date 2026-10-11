using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Evidence.Core.Ids;

/// <summary>
/// RunInstanceId: one invocation of 'run', new every time (an IIdGenerator Guid, written "D"). Unlike
/// <see cref="RunId"/> (the run directory's name, which a rerun in the same directory repeats), it tells two runs apart.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RunInstanceId>))]
public readonly partial struct RunInstanceId : ITypedId<RunInstanceId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("RunInstanceId must not be blank") : Validation.Ok;
}
