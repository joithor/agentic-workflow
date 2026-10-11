using Aw.Evidence.Core.Guard;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The ISecretsSource contract: a set secret reads back; a blank or missing one is null.</summary>
public abstract class SecretsSourceContract
{
    /// <summary>Creates a source holding <paramref name="values"/> by name.</summary>
    /// <param name="values">The secrets.</param>
    /// <returns>The source.</returns>
    protected abstract ISecretsSource Create(IReadOnlyDictionary<string, string> values);

    /// <summary>A set secret is its value; a blank one and a missing one are null.</summary>
    [Fact]
    public void A_set_secret_reads_back_and_a_blank_or_missing_one_is_null()
    {
        var secrets = Create(new Dictionary<string, string>(StringComparer.Ordinal) { ["QA_EMAIL"] = "qa@example.com", ["QA_PASSWORD"] = "  " });

        Expect.That(secrets.Get("QA_EMAIL") == "qa@example.com", "a set secret is its value");
        Expect.That(secrets.Get("QA_PASSWORD") is null, "a blank secret is unset");
        Expect.That(secrets.Get("MISSING") is null, "a missing secret is unset");
    }
}
