using System.Diagnostics;
using Aw.Kernel.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class SpanTests
{
    [Fact]
    public void Exporting_is_off_by_default()
    {
        using var h = new Harness();
        using (var span = h.Source.StartActivity("work"))
        {
            _ = span?.SetTag("k", "v");
        }

        h.Runtime.Flush();

        Assert.False(h.Runtime.IsExporting);
        Assert.Equal(0, h.Runtime.ExportProcessorCount);
    }

    [Fact]
    public void Spans_are_recorded_without_an_exporter_so_logs_still_carry_ids()
    {
        using var h = new Harness();

        using var span = h.Source.StartActivity("work");

        Assert.NotNull(span);
        Assert.True(span.Recorded);
    }

    [Fact]
    public void With_an_exporter_a_clean_span_is_exported_unchanged()
    {
        using var h = new Harness(export: true);
        using (var span = h.Source.StartActivity("work"))
        {
            _ = span?.SetTag("repo", "agentic-workflow");
        }

        h.Runtime.Flush();

        Assert.True(h.Runtime.IsExporting);
        var exported = Assert.Single(h.Spans!.Exported);
        Assert.Equal("work", exported.DisplayName);
        Assert.Equal("agentic-workflow", exported.GetTagItem("repo"));
    }

    [Fact]
    public void A_secret_in_a_tag_name_or_status_is_redacted_before_export()
    {
        using var h = new Harness(export: true);
        using (var span = h.Source.StartActivity("work"))
        {
            _ = span!.SetTag("header", Secrets.Bearer);
            span.DisplayName = "call " + Secrets.GithubToken;
            _ = span.SetStatus(ActivityStatusCode.Error, "denied " + Secrets.EnvPair);
        }

        var everything = h.Everything();

        _ = Assert.Single(h.Spans!.Exported);
        Assert.Contains("[REDACTED:", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(Secrets.Body, everything, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_with_a_secret_name_is_withheld()
    {
        using var h = new Harness(export: true);
        using (var span = h.Source.StartActivity("work"))
        {
            _ = span!.SetTag("api_key", "plain-looking-value");
        }

        h.Runtime.Flush();

        Assert.Equal("[REDACTED:secret-field]", Assert.Single(h.Spans!.Exported).GetTagItem("api_key"));
    }

    [Fact]
    public void A_span_whose_event_carries_a_secret_is_dropped_and_counted()
    {
        using var h = new Harness(export: true);
        using (var span = h.Source.StartActivity("work"))
        {
            _ = span!.AddEvent(new ActivityEvent("sent", tags: new ActivityTagsCollection { ["body"] = Secrets.Bearer }));
        }

        h.Runtime.Flush();

        Assert.Empty(h.Spans!.Exported);
        Assert.Equal(1, h.Runtime.Withheld.Spans);
    }

    [Fact]
    public void A_span_whose_scrub_is_blocked_is_dropped_and_counted()
    {
        using var h = new Harness(export: true, redactor: new BlockingRedactor());
        using (var span = h.Source.StartActivity("work"))
        {
            _ = span!.SetTag("note", BlockingRedactor.Marker);
        }

        h.Runtime.Flush();

        Assert.Empty(h.Spans!.Exported);
        Assert.Equal(1, h.Runtime.Withheld.Spans);
    }
}
