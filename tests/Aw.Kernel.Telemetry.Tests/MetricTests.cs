using Aw.Kernel.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class MetricTests
{
    [Fact]
    public void A_metric_with_clean_tags_is_exported()
    {
        using var h = new Harness(export: true);
        var counter = h.Meter.CreateCounter<long>("aw.test.items");

        counter.Add(3, new KeyValuePair<string, object?>("kind", "feature"));
        h.Runtime.Flush();

        var metric = Assert.Single(h.Metrics!.Exported, m => m.Name == "aw.test.items");
        Assert.Equal("feature", Assert.Single(metric.PointTags)["kind"]);
    }

    [Fact]
    public void A_metric_stream_with_a_secret_tag_value_is_dropped_and_counted()
    {
        using var h = new Harness(export: true);
        var counter = h.Meter.CreateCounter<long>("aw.test.leaky");

        counter.Add(1, new KeyValuePair<string, object?>("header", Secrets.Bearer));
        var everything = h.Everything();

        Assert.DoesNotContain(h.Metrics!.Exported, m => m.Name == "aw.test.leaky");
        Assert.DoesNotContain(Secrets.Body, everything, StringComparison.Ordinal);
        Assert.Equal(1, h.Runtime.Withheld.Metrics);
    }

    [Fact]
    public void Without_a_metric_exporter_nothing_is_collected()
    {
        using var h = new Harness();
        var counter = h.Meter.CreateCounter<long>("aw.test.quiet");

        counter.Add(1);
        h.Runtime.Flush();

        Assert.False(counter.Enabled);
    }

    [Fact]
    public void A_metric_stream_whose_scrub_is_blocked_is_dropped_and_counted()
    {
        using var h = new Harness(export: true, redactor: new BlockingRedactor());
        var counter = h.Meter.CreateCounter<long>("aw.test.slow");

        counter.Add(1, new KeyValuePair<string, object?>("note", BlockingRedactor.Marker));
        h.Runtime.Flush();

        Assert.DoesNotContain(h.Metrics!.Exported, m => m.Name == "aw.test.slow");
        Assert.Equal(1, h.Runtime.Withheld.Metrics);
    }

    [Fact]
    public void The_withheld_counter_is_exported_with_its_path()
    {
        using var h = new Harness(export: true);
        var counter = h.Meter.CreateCounter<long>("aw.test.leaky2");

        counter.Add(1, new KeyValuePair<string, object?>("header", Secrets.Bearer));
        h.Runtime.Flush();
        h.Runtime.Flush();

        var withheld = Assert.Single(h.Metrics!.Exported, m => m.Name == "aw.telemetry.scrub.withheld");
        Assert.Contains(withheld.PointTags, p => p["path"] == "metric");
    }
}
