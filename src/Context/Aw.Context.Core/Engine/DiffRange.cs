using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The diff shape's range: two commit shas (7 to 64 lower-case hex characters), the changes measured from their merge base
/// to the second. Refs only: a branch, a tag, HEAD, '~', '^' or a '...' range is refused, because resolving those names is
/// the caller's job (the engine makes no forge calls and reads no refs).
/// </summary>
/// <param name="Base">The first commit's sha.</param>
/// <param name="Head">The second commit's sha.</param>
[JsonConverter(typeof(DiffRangeJsonConverter))]
public sealed partial record DiffRange(string Base, string Head)
{
    /// <summary>What the 006 for a malformed range says.</summary>
    public const string Message = "a diff names two commit shas joined by '..' (base..head, 7 to 64 lower-case hex characters each); resolve the refs to shas first: branch names, tags, HEAD, '~', '^' and '...' are not accepted";

    /// <summary>Reads a range written base..head.</summary>
    /// <param name="text">The range as a transport carries it.</param>
    /// <returns>The range, or SND-CONTEXT-006.</returns>
    public static Result<DiffRange> Parse(string? text)
    {
        var match = text is null ? null : Shape.Match(text);
        return match is { Success: true }
            ? new DiffRange(match.Groups["base"].Value, match.Groups["head"].Value)
            : ContextErrors.QueryInvalid.With(Message);
    }

    /// <summary>Whether both sides are shas.</summary>
    /// <returns>True when this range would parse.</returns>
    public bool IsValid() => Sha.IsMatch(Base) && Sha.IsMatch(Head);

    /// <inheritdoc/>
    public override string ToString() => Base + ".." + Head;

    [GeneratedRegex("^(?<base>[0-9a-f]{7,64})\\.\\.(?<head>[0-9a-f]{7,64})\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    [GeneratedRegex("^[0-9a-f]{7,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha { get; }
}
