using Aw.Context.Core.Trust;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract the egress and ingest ports honour, run against <see cref="FakeScrubber"/> and the Redaction adapters.
/// A binding names a secret its implementation recognizes and the placeholder prefix it writes.
/// </summary>
/// <typeparam name="TScrubber">The implementation (both ports).</typeparam>
public abstract class ScrubberContract<TScrubber>
    where TScrubber : IEgressScrubber, IIngestFilter
{
    /// <summary>A secret the implementation redacts.</summary>
    protected abstract string Secret { get; }

    /// <summary>A fresh implementation.</summary>
    /// <returns>The scrubber.</returns>
    protected abstract TScrubber Create();

    /// <summary>Clean text passes unchanged on both ports.</summary>
    [Fact]
    public void Clean_text_passes_unchanged()
    {
        var scrubber = Create();

        Check(scrubber.Scrub("split pane orientation").Text == "split pane orientation", "egress keeps clean text");
        Check(scrubber.Clean("split pane orientation", singleLine: false).Text == "split pane orientation", "ingest keeps clean text");
    }

    /// <summary>A secret never comes back, and the text around it does.</summary>
    [Fact]
    public void A_secret_is_replaced_by_a_placeholder()
    {
        var scrubbed = Create().Scrub("key " + Secret + " end");

        Check(!scrubbed.Text.Contains(Secret, StringComparison.Ordinal) && scrubbed.Text.Contains("[REDACTED:", StringComparison.Ordinal), "the secret is a placeholder");
        Check(scrubbed.Text.StartsWith("key ", StringComparison.Ordinal) && scrubbed.Redacted, "the text around it stays and it says so");
    }

    /// <summary>Ingest drops HTML comments and collapses a single-line field.</summary>
    [Fact]
    public void Ingest_drops_comments_and_collapses_titles()
    {
        var title = Create().Clean("A\n  <!-- hidden -->title\t here", singleLine: true);

        Check(title.Text == "A title here", $"a title collapses to one line without comments (got '{title.Text}')");
    }

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TScrubber).Name}: {what}");
        }
    }
}
