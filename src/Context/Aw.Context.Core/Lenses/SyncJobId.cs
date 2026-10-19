using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Lenses;

/// <summary>The id of a sync job as Sindri's jobs view serves it ('JOB-…'); the engine only carries it.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SyncJobId>))]
public readonly partial struct SyncJobId : ITypedId<SyncJobId, string>
{
    private static Validation Validate(string value) =>
        value.Length is >= 1 and <= 64 && value.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-') ? Validation.Ok : Validation.Invalid("A job id is 1 to 64 letters, digits or '-'");
}
