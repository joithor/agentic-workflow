using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Packets.Core.Model;

/// <summary>Where a freeze record lives, relative to the packets/ namespace: '&lt;packet&gt;/frozen/&lt;hex&gt;.json'.</summary>
public static partial class FreezeLocation
{
    /// <summary>The location of <paramref name="record"/>.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The relative path, or AW-PACKETS-001 when the hash isn't 'sha256:&lt;64 lower-case hex digits&gt;'.</returns>
    public static Result<string> Of(FreezeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var match = Sha256.Match(record.Hash);
        return match.Success
            ? $"{record.Packet.Value}/frozen/{match.Groups["hex"].Value}.json"
            : PacketsErrors.Invalid.With($"'{record.Hash}' isn't a sha256 hash.");
    }

    [GeneratedRegex("^sha256:(?<hex>[0-9a-f]{64})$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha256 { get; }
}
