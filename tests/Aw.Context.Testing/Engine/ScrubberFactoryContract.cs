using Aw.Context.Core;
using Aw.Context.Core.Trust;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IScrubberFactory"/> honours, run against <see cref="FakeScrubberFactory"/> and the
/// Redaction factory: an extra pattern is scrubbed on both ports with its own placeholder, the ruleset follows the
/// list, and a refused entry is SND-CONTEXT-001 at its key path.
/// </summary>
/// <typeparam name="TFactory">The implementation.</typeparam>
public abstract class ScrubberFactoryContract<TFactory>
    where TFactory : IScrubberFactory
{
    /// <summary>The spec's example pattern (§8.8).</summary>
    protected static ScrubPattern Acme { get; } = new("acme-customer-id", "ACME-[0-9]{6}");

    /// <summary>A fresh factory.</summary>
    /// <returns>The factory.</returns>
    protected abstract TFactory Create();

    /// <summary>An extra pattern's match becomes '[REDACTED:&lt;id&gt;]' on egress and on ingest.</summary>
    [Fact]
    public void An_extra_pattern_is_scrubbed_on_both_ports()
    {
        var scrubbers = Expect(Create().For([Acme]));

        var egress = scrubbers.Egress.Scrub("order ACME-123456 shipped");
        var ingest = scrubbers.Ingest.Clean("order ACME-123456\nshipped", singleLine: false);

        Check(egress.Text == "order [REDACTED:acme-customer-id] shipped" && egress.Redacted, $"egress redacts the id (got '{egress.Text}')");
        Check(ingest.Text == "order [REDACTED:acme-customer-id]\nshipped" && ingest.Redacted, $"ingest redacts the id and keeps the lines (got '{ingest.Text}')");
    }

    /// <summary>No extra pattern leaves such an id alone.</summary>
    [Fact]
    public void Without_the_pattern_the_id_stays() =>
        Check(Expect(Create().For([])).Egress.Scrub("order ACME-123456").Text == "order ACME-123456", "no extra pattern, no redaction");

    /// <summary>The ruleset is the same for the same list and differs for another.</summary>
    [Fact]
    public void The_ruleset_follows_the_list()
    {
        var factory = Create();

        var none = Expect(factory.For([])).Ruleset;
        var acme = Expect(factory.For([Acme])).Ruleset;
        var again = Expect(Create().For([new ScrubPattern("acme-customer-id", "ACME-[0-9]{6}")])).Ruleset;
        var other = Expect(factory.For([new ScrubPattern("acme-customer-id", "ACME-[0-9]{7}")])).Ruleset;

        Check(none != acme && acme == again && acme != other, "the ruleset changes with the patterns, and only with them");
    }

    /// <summary>A refused pattern or id is 001 at its own key path, after the accepted entries before it.</summary>
    [Fact]
    public void A_refused_entry_is_001_at_its_key_path()
    {
        var factory = Create();

        var pattern = factory.For([Acme, new ScrubPattern("order-id", "ORD-[0-9")]);
        var id = factory.For([new ScrubPattern("Not An Id", "X-[0-9]{3}")]);
        var repeated = factory.For([Acme, Acme]);

        Check(Path(pattern) == "context.scrub.extraPatterns[1].pattern:", $"a pattern that doesn't parse (got '{Path(pattern)}')");
        Check(Path(id) == "context.scrub.extraPatterns[0].id:", $"an id that isn't one (got '{Path(id)}')");
        Check(Path(repeated) == "context.scrub.extraPatterns[1].id:", $"a repeated id (got '{Path(repeated)}')");
    }

    private static string Path(Kernel.Result<Scrubbers> result) =>
        result.IsSuccess || result.Error.Code != ContextErrors.SettingsInvalid.Code ? "accepted" : result.Error.Message[..(result.Error.Message.IndexOf(':', StringComparison.Ordinal) + 1)];

    private static Scrubbers Expect(Kernel.Result<Scrubbers> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{typeof(TFactory).Name}: {result.Error.Code} {result.Error.Message}");

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TFactory).Name}: {what}");
        }
    }
}
