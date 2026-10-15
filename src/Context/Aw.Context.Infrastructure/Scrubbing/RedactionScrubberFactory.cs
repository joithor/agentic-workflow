using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// Builds a call's scrubbers over Aw.Redaction (spec §7, D14): the built-in rules, then the profile's extra patterns,
/// compiled with the default match timeout and the whole-scan deadline on the host's clock. Redaction checks the
/// patterns; each refusal maps to SND-CONTEXT-001 at its key path ('context.scrub.extraPatterns[0].pattern: …', the
/// message never quotes the pattern). The ruleset comes from the built redactor's digest. The last build is kept,
/// keyed by the patterns as written, so calls under an unchanged profile don't compile again.
/// </summary>
/// <param name="clock">The host's clock (Redaction reads none of its own).</param>
internal sealed class RedactionScrubberFactory(TimeProvider clock) : IScrubberFactory
{
    private Built? _last;   // one reference, swapped whole: calls on other threads see the old build or the new one

    public Result<Scrubbers> For(IReadOnlyList<ScrubPattern> extraPatterns)
    {
        ArgumentNullException.ThrowIfNull(extraPatterns);
        var key = Key(extraPatterns);
        if (Volatile.Read(ref _last) is { } last && string.Equals(last.Key, key, StringComparison.Ordinal))
        {
            return last.Scrubbers;
        }

        // why: Redaction refuses a null id or pattern itself (AW-REDACT-002, -003), so a missing one is reported at its key path like any other
        var created = Redactor.Create([.. extraPatterns.Select(p => new ExtraPattern(p.Rule!, p.Pattern!))], Redactor.DefaultMatchTimeout, clock);
        if (!created.IsValid)
        {
            var refused = created.Errors[0];
            var field = refused.Field == ExtraPatternField.Id ? "id" : "pattern";
            return ContextErrors.SettingsInvalid.With(string.Create(CultureInfo.InvariantCulture, $"context.scrub.extraPatterns[{refused.Index}].{field}: {refused.Message}"));
        }

        var scrubber = new RedactionScrubber(created.Redactor);
        var scrubbers = new Scrubbers(scrubber, scrubber, Ruleset(created.Redactor.RulesDigest));
        Volatile.Write(ref _last, new Built(key, scrubbers));
        return scrubbers;
    }

    /// <summary>
    /// The ruleset: a hash of the sanitizer's version and rules and the built redactor's digest (Redaction's own
    /// fingerprint of every rule it applies, extra patterns included, with their options and the scan engine).
    /// </summary>
    internal static ScrubRuleset Ruleset(string rulesDigest)
    {
        var text = new StringBuilder().Append(CultureInfo.InvariantCulture, $"sanitizer:{IngestSanitizer.Version}\n");
        _ = Part(Part(text, IngestSanitizer.Rules), rulesDigest);
        return ScrubRuleset.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32]);
    }

    /// <summary>The cache key: every (id, pattern) as written, in order, each part length-prefixed so no two lists collide.</summary>
    internal static string Key(IReadOnlyList<ScrubPattern> extraPatterns)
    {
        var text = new StringBuilder();
        foreach (var pattern in extraPatterns)
        {
            _ = Part(Part(text, pattern.Rule), pattern.Pattern);
        }

        return text.ToString();
    }

    private static StringBuilder Part(StringBuilder text, string? value) =>
        value is null ? text.Append("-1:\n") : text.Append(CultureInfo.InvariantCulture, $"{value.Length}:").Append(value).Append('\n');

    private sealed record Built(string Key, Scrubbers Scrubbers);
}
