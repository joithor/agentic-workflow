using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A job's id (API spec §3.1 'GET /jobs/{jobId}'): 'JOB-' and 32 lowercase hex digits, a GUIDv7 in 'N' format.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<JobId>))]
public readonly partial struct JobId : ITypedId<JobId, string>
{
    private const string Prefix = "JOB-";

    /// <summary>A new id from the id port.</summary>
    /// <param name="ids">The id port (a GUIDv7 in production, a sequence in tests).</param>
    /// <returns>The id.</returns>
    public static JobId New(IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return From(Prefix + ids.NewId().ToString("N", CultureInfo.InvariantCulture));
    }

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A job id is 'JOB-' and 32 lowercase hex digits");

    [GeneratedRegex("^JOB-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
