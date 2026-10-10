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
}
