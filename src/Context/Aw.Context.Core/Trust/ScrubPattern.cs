namespace Aw.Context.Core.Trust;

/// <summary>
/// One extra scrub pattern from the profile ('context.scrub.extraPatterns', spec §8.8, D14), as written. Core can't
/// check it (the regex rules belong to Aw.Redaction), so <see cref="IScrubberFactory.For"/> checks it and refuses it with
/// its key path.
/// </summary>
/// <param name="Rule">The rule id its placeholder names ('[REDACTED:&lt;id&gt;]'), or null when the entry has none.</param>
/// <param name="Pattern">The .NET regex, or null when the entry has none.</param>
public sealed record ScrubPattern(string? Rule, string? Pattern);
