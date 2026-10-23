using System.Globalization;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The fields of a digest the watcher compares (the same as <see cref="PullDigest"/>'s, without the link).</summary>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadSha">The head commit.</param>
/// <param name="Checks">The checks' state.</param>
/// <param name="CommentCount">How many comments and reviews.</param>
/// <param name="Version">The forge's own version token.</param>
internal sealed record WatchedDigest(PullState State, string HeadSha, PullChecksState Checks, int CommentCount, string Version)
{
    /// <summary>The comparable part of a digest.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>Its fields.</returns>
    internal static WatchedDigest Of(PullDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        return new(digest.State, digest.HeadSha, digest.Checks, digest.CommentCount, digest.Version);
    }

    /// <summary>The digest on one line: what a change event's idempotency key is made of.</summary>
    /// <returns>The line.</returns>
    internal string Line() => string.Create(CultureInfo.InvariantCulture, $"{State}/{HeadSha}/{Checks}/{CommentCount}/{Version}");
}
