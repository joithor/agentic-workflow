using Aw.Evidence.Core.Data;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The ITextScrubber contract: a secret of a known shape does not survive, and ordinary text, a SHA-256 and a GUID
/// (the run's own identifiers) come back unchanged.
/// </summary>
public abstract class TextScrubberContract
{
    // A GitHub-token-shaped secret every real rule set knows, assembled so no token-like literal sits in the source.
    /// <summary>The secret the scrubber made by <see cref="Create"/> hides.</summary>
    protected static string Secret { get; } = "gh" + "p_" + new string('a', 36);

    /// <summary>Makes a scrubber that hides <see cref="Secret"/>.</summary>
    /// <returns>The scrubber.</returns>
    protected abstract ITextScrubber Create();

    /// <summary>A secret is replaced wherever it sits in the text.</summary>
    [Fact]
    public void A_secret_does_not_survive()
    {
        var scrubbed = Create().Scrub($"header Authorization: token {Secret} sent");

        Expect.That(!scrubbed.Contains(Secret, StringComparison.Ordinal), "the secret is gone");
        Expect.That(scrubbed.StartsWith("header ", StringComparison.Ordinal), "the text around it stays");
    }

    /// <summary>Ordinary text and the run's identifiers are not touched.</summary>
    [Fact]
    public void Ordinary_text_a_sha256_and_a_guid_come_back_unchanged()
    {
        const string text = "GET /api/notes: status 200, sha 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08 run 00000000-0000-0000-0000-000000000001";

        Expect.That(Create().Scrub(text) == text, "nothing in it is a secret");
    }
}
