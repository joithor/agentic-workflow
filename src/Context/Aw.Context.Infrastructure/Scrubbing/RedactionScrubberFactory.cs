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
/// message never quotes the pattern). The last build is kept, so calls under an unchanged profile don't compile again.
/// </summary>
/// <param name="clock">The host's clock (Redaction reads none of its own).</param>
internal sealed class RedactionScrubberFactory(TimeProvider clock) : IScrubberFactory
{
    private Scrubbers? _last;   // one reference, swapped whole: calls on other threads see the old build or the new one

    public Result<Scrubbers> For(IReadOnlyList<ScrubPattern> extraPatterns)
    {
        ArgumentNullException.ThrowIfNull(extraPatterns);
        var ruleset = Fingerprint([]);
        if (Volatile.Read(ref _last) is { } last && last.Ruleset == ruleset)
        {
            return last;
        }

        // why: Redaction refuses a null id or pattern itself (AW-REDACT-002, -003), so a missing one is reported at its key path like any other
        var created = Redactor.Create([.. extraPatterns.Take(0).Select(p => new ExtraPattern(p.Rule!, p.Pattern!))], Redactor.DefaultMatchTimeout, clock);
        if (!created.IsValid)
        {
            var refused = created.Errors[0];
            var field = refused.Field == ExtraPatternField.Id ? "id" : "pattern";
            return ContextErrors.SettingsInvalid.With(string.Create(CultureInfo.InvariantCulture, $"context.scrub.extraPatterns[{refused.Index}].{field}: {refused.Message}"));
        }

        var scrubber = new RedactionScrubber(created.Redactor);
        var scrubbers = new Scrubbers(scrubber, scrubber, ruleset);
        Volatile.Write(ref _last, scrubbers);
        return scrubbers;
    }

    /// <summary>
    /// The ruleset: a hash of the sanitizer's version, the built-in rule ids and every (id, pattern) in order, each part
    /// length-prefixed so no two lists hash alike.
    /// </summary>
    internal static ScrubRuleset Fingerprint(IReadOnlyList<ScrubPattern> extraPatterns)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"sanitizer:{IngestSanitizer.Version}\n")
            .Append("rules:").AppendJoin(',', Redactor.Default.RuleIds).Append('\n');
        foreach (var pattern in extraPatterns)
        {
            _ = Part(Part(text, pattern.Rule), pattern.Pattern);
        }

        return ScrubRuleset.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32]);
    }

    private static StringBuilder Part(StringBuilder text, string? value) =>
        value is null ? text.Append("-1:\n") : text.Append(CultureInfo.InvariantCulture, $"{value.Length}:").Append(value).Append('\n');
}
