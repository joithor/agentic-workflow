using Aw.Sindri.Core.Text;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="ITextScrubber"/> honours: a token-shaped secret never survives, and plain text comes back unchanged. Run against the fake and the Aw.Redaction adapter.</summary>
public abstract class TextScrubberContract
{
    /// <summary>A token-shaped secret (a GitHub personal access token's shape).</summary>
    protected const string Secret = "ghp_" + "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8";

    /// <summary>Runs first in the secret case: a binding whose scrubber recognises no secret (the pass-through fake) overrides it to skip, with Assert.Skip.</summary>
    protected virtual void SkipUnlessItRecognisesSecrets()
    {
    }

    /// <summary>A fresh scrubber.</summary>
    /// <returns>The scrubber.</returns>
    protected abstract ITextScrubber Create();

    /// <summary>A token in the middle of a sentence is replaced.</summary>
    [Fact]
    public void Scrub_removes_a_token_shaped_secret()
    {
        SkipUnlessItRecognisesSecrets();

        var scrubbed = Create().Scrub("deploy with " + Secret + " today");

        Expect.True(!scrubbed.Contains(Secret, StringComparison.Ordinal));
        Expect.True(scrubbed.StartsWith("deploy with ", StringComparison.Ordinal));
    }

    /// <summary>Text with nothing secret in it comes back as it was.</summary>
    [Fact]
    public void Scrub_keeps_plain_text()
    {
        const string plain = "Try the queue: ENG-142 is next.";

        Expect.Equal(plain, Create().Scrub(plain));
    }
}
