namespace Aw.Sindri.Core.Sessions;

/// <summary>What <see cref="PreviewRules"/> makes of a capture (collector spec CA-9): the lines to show and whether anything was cut.</summary>
/// <param name="Lines">At most 40 lines of at most 200 UTF-16 code units, with escapes and control characters stripped.</param>
/// <param name="Truncated">True when a line was cut at 200 or the 64 KiB cap dropped output; the last-40 window alone never sets it.</param>
public sealed record PreviewText(IReadOnlyList<string> Lines, bool Truncated);
