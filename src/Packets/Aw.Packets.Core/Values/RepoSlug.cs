using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Values;

/// <summary>
/// RepoSlug: a repository as 'owner/name'. Each side is letters, digits, '.', '_' and '-', never ends in '.' (so it is
/// never '.' or '..', and Windows can't strip it), and isn't a Windows device name (con, prn, aux, nul, com1-9,
/// lpt1-9, with or without an extension): the slug is also two directory names under packets/ on every OS.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RepoSlug>))]
public readonly partial struct RepoSlug : ITypedId<RepoSlug, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) && !value.Split('/').Any(IsDeviceName)
            ? Validation.Ok
            : Validation.Invalid("RepoSlug must be 'owner/name': letters, digits, '.', '_', '-', not ending in '.', not a device name");

    private static bool IsDeviceName(string side)
    {
        var stem = side.Split('.')[0];
        return DeviceName.IsMatch(stem);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]*[A-Za-z0-9_-]/[A-Za-z0-9._-]*[A-Za-z0-9_-]\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    [GeneratedRegex("^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])\\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DeviceName { get; }
}
