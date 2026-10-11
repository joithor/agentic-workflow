using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// A deadline's id, which is also its trigger id (spec §5.1, §5.4): '{entry}:{kind}:{arm}'. The arm counts per frame
/// entry and kind, and rises on every re-arm (W16), so a deadline that fired under an earlier arm can never fire again.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<DeadlineId>))]
public readonly partial struct DeadlineId : ITypedId<DeadlineId, string>
{
    /// <summary>The frame entry it was armed in.</summary>
    public int Entry => int.Parse(Value.AsSpan(0, Value.IndexOf(':', StringComparison.Ordinal)), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>Its kind.</summary>
    public DeadlineKind Kind => Value.Split(':')[1] switch
    {
        "state" => DeadlineKind.State,
        "retry" => DeadlineKind.Retry,
        _ => DeadlineKind.TakeoverIdle,
    };

    /// <summary>Its arm, from 1.</summary>
    public int Arm => int.Parse(Value.AsSpan(Value.LastIndexOf(':') + 1), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The id '{entry}:{kind}:{arm}'.</summary>
    /// <param name="entry">The frame entry, from 1.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="arm">The arm, from 1.</param>
    /// <returns>The id.</returns>
    public static DeadlineId For(int entry, DeadlineKind kind, int arm)
    {
        var name = kind switch
        {
            DeadlineKind.State => "state",
            DeadlineKind.Retry => "retry",
            DeadlineKind.TakeoverIdle => "takeoverIdle",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "An unknown deadline kind"),
        };
        return From(string.Create(CultureInfo.InvariantCulture, $"{entry}:{name}:{arm}"));
    }

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A deadline id is '{entry}:{state|retry|takeoverIdle}:{arm}'");

    [GeneratedRegex("^[1-9][0-9]{0,8}:(state|retry|takeoverIdle):[1-9][0-9]{0,8}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
