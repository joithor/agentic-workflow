namespace Aw.Packets.Core.Checks;

/// <summary>What the scope rules know about the base commit: its files, or why they can't be listed.</summary>
/// <param name="Files">The files at the base commit; null when the commit can't be used.</param>
/// <param name="Problem">Why the commit can't be used (a PKT-BASE-001 failure); null when <paramref name="Files"/> is set.</param>
internal sealed record BaseTree(IReadOnlySet<string>? Files, string? Problem);
