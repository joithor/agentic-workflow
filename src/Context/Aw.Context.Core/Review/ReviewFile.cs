using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Aw.Context.Core.Review;

/// <summary>The review file 'map review --export' writes and '--apply' reads (S-4): the queue as entries a person fills in with a decision.</summary>
/// <param name="Draft">The draft the file was exported from ('v14@7c3e1a2').</param>
/// <param name="Entries">One per undecided entry.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewFile(string Draft, IReadOnlyList<ReviewFileEntry> Entries)
{
    /// <summary>The hash of the file's entries, in order: what a protected apply binds its confirmation to.</summary>
    /// <returns>The hash, as lower-case hex.</returns>
    public string Fingerprint()
    {
        var text = new StringBuilder(Draft);
        foreach (var e in Entries)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"\n{e.Label}|{e.Group}|{e.Confidence:R}|{e.Node?.Value}|{e.Link?.Value}|{e.Decision}|{e.Other?.Value}");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>The file for a queue: every entry undecided.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns>The file.</returns>
    public static ReviewFile Of(ReviewQueueView queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return new ReviewFile(queue.Draft, [.. queue.Entries.Where(e => e.Group != ReviewGroup.Orphaned).Select(e =>
            new ReviewFileEntry(e.Label, e.Group, e.Confidence, e.Link is null ? e.Node : null, e.Link))]);
    }
}
