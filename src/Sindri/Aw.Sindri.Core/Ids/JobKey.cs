using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A job's idempotency key within its kind: what two starts must differ in to run together (a service's name for 'RunService'). 1 to 80 characters, lowercase.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<JobKey>))]
public readonly partial struct JobKey : ITypedId<JobKey, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A job key is 1 to 80 lowercase letters, digits, '.', '_', ':', '/' or '-', starting with a letter or digit");

    [GeneratedRegex("^[a-z0-9][a-z0-9._:/-]{0,79}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
