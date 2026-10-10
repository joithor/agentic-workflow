using System.Globalization;
using Aw.Redaction;

namespace Aw.Kernel.Telemetry;

/// <summary>Applies the redactor to telemetry text and remembers whether anything in one record was found or blocked.</summary>
internal sealed class Scrubber(IRedactor redactor)
{
    /// <summary>Scrubs <paramref name="text"/>; the caller learns whether it was redacted or blocked.</summary>
    public string Scrub(string text, ref ScrubTally tally)
    {
        var result = redactor.Scrub(text);
        tally = tally.With(result.Outcome);
        return result.Text;
    }

    /// <summary>A named value: withheld whole when the name is a secret's, otherwise scrubbed as text.</summary>
    public string ScrubField(string name, object? value, ref ScrubTally tally)
    {
        if (redactor.IsSecretName(name))
        {
            tally = tally.With(ScrubOutcome.Redacted);
            return ScrubResult.SecretFieldText;
        }

        return Scrub(Text(value), ref tally);
    }

    /// <summary>Whether <paramref name="name"/> is a secret's name (password, api_key, …).</summary>
    public bool IsSecretName(string name) => redactor.IsSecretName(name);

    /// <summary>The invariant text of a value.</summary>
    public static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
