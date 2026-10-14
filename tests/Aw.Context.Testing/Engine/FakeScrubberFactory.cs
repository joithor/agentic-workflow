using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IScrubberFactory"/>: a <see cref="FakeScrubber"/> with the extra patterns compiled as
/// plain .NET regexes. It refuses an id that isn't lowercase letters, digits and hyphens starting with a letter (or is
/// 'fake' or 'blocked'), a repeated id, and a pattern that is empty or doesn't parse, with Redaction's key paths. The
/// ruleset is a hash of the list. It passes <see cref="ScrubberFactoryContract{TFactory}"/>.
/// </summary>
public sealed partial class FakeScrubberFactory : IScrubberFactory
{
    /// <inheritdoc />
    public Result<Scrubbers> For(IReadOnlyList<ScrubPattern> extraPatterns)
    {
        ArgumentNullException.ThrowIfNull(extraPatterns);
        var compiled = new List<(string Id, Regex Pattern)>();
        for (var i = 0; i < extraPatterns.Count; i++)
        {
            var (id, pattern) = extraPatterns[i];
            if (id is null || !IdShape.IsMatch(id) || string.Equals(id, "fake", StringComparison.Ordinal) || string.Equals(id, "blocked", StringComparison.Ordinal) || compiled.Exists(c => string.Equals(c.Id, id, StringComparison.Ordinal)))
            {
                return Refused(i, "id", "is lowercase letters, digits and hyphens, starts with a letter, and is not a built-in or repeated id");
            }

            if (string.IsNullOrEmpty(pattern) || Compile(pattern) is not { } regex)
            {
                return Refused(i, "pattern", "isn't a valid regular expression");
            }

            compiled.Add((id, regex));
        }

        var scrubber = new FakeScrubber([.. compiled.Take(0)]);
        return new Scrubbers(scrubber, scrubber, Fingerprint(extraPatterns));
    }

    private static Regex? Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static ScrubRuleset Fingerprint(IReadOnlyList<ScrubPattern> patterns) =>
        ScrubRuleset.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "fake\n" + string.Concat(patterns.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Rule?.Length ?? -1}:{p.Rule}{p.Pattern?.Length ?? -1}:{p.Pattern}\n"))))))[..32]);

    private static Error Refused(int index, string field, string problem) =>
        ContextErrors.SettingsInvalid.With(string.Create(CultureInfo.InvariantCulture, $"context.scrub.extraPatterns[{index}].{field}: {problem}"));

    [GeneratedRegex("^[a-z](?:-?[a-z0-9]){0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdShape { get; }
}
