using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Map;

/// <summary>The caller-based fence (provisional owner default, 2026-10-14 night; context spec §7): who reads clean text beside a trust field and who reads the fence.</summary>
public sealed class FencePolicyTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(ContextCallerKind.Human, ContextClient.Cli, FencePolicy.Clean)]
    [InlineData(ContextCallerKind.Human, ContextClient.Http, FencePolicy.Clean)]
    [InlineData(ContextCallerKind.Human, ContextClient.Mcp, FencePolicy.Fenced)]
    [InlineData(ContextCallerKind.Human, ContextClient.Service, FencePolicy.Fenced)]
    [InlineData(ContextCallerKind.Session, ContextClient.Cli, FencePolicy.Fenced)]
    [InlineData(ContextCallerKind.Session, ContextClient.Http, FencePolicy.Fenced)]
    [InlineData(ContextCallerKind.Session, ContextClient.Mcp, FencePolicy.Fenced)]
    [InlineData(ContextCallerKind.Service, ContextClient.Service, FencePolicy.Fenced)]
    public void Only_a_human_at_the_cli_or_in_the_app_reads_clean_text(ContextCallerKind kind, ContextClient client, FencePolicy expected) =>
        Assert.Equal(expected, FencePolicies.For(new ContextCaller(kind, Repo: null, client)));

    [Fact, Trait("Lane", "unit")]
    public void A_clean_text_leaves_an_untrusted_label_unwrapped_and_a_fenced_one_wraps_it()
    {
        var scrubbers = new FakeScrubberFactory().For([]).Value;

        var clean = new MapText(scrubbers, FencePolicy.Clean).Label("Model said so", Trust.Untrusted);
        var fenced = new MapText(scrubbers, FencePolicy.Fenced).Label("Model said so", Trust.Untrusted);
        var defaulted = new MapText(scrubbers).Label("Model said so", Trust.Untrusted);
        var trusted = new MapText(scrubbers, FencePolicy.Fenced).Label("A", Trust.Trusted);

        Assert.Equal("Model said so", clean);
        Assert.Equal("<untrusted source=\"map\">Model said so</untrusted>", fenced);
        Assert.Equal(fenced, defaulted);
        Assert.Equal("A", trusted);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_clean_body_is_still_scrubbed()
    {
        var scrubbers = new FakeScrubberFactory().For([]).Value;

        var body = new MapText(scrubbers, FencePolicy.Clean).Body("quote " + FakeScrubber.Secret, Trust.Untrusted);

        Assert.Equal("quote [REDACTED:fake]", body);
    }
}
