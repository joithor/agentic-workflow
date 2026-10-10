using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class RobustnessTests
{
    [Fact]
    public void A_sink_that_throws_never_reaches_the_caller_and_is_counted()
    {
        using var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = "aw-demo", Sink = new ThrowingSink(), SourcePattern = "Aw.Test.Robust" });
        var logger = runtime.LoggerFactory.CreateLogger("Aw.Test");

        logger.Emit(LogLevel.Information, "hello");
        runtime.Flush();

        Assert.Equal(2, runtime.Withheld.Failures);
    }

    [Fact]
    public void Disposing_after_the_host_closed_the_writer_does_not_throw()
    {
        var writer = new StringWriter();
        var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = "aw-demo", Sink = new TextWriterLogSink(writer), SourcePattern = "Aw.Test.Robust" });
        writer.Dispose();

        runtime.LoggerFactory.CreateLogger("Aw.Test").Emit(LogLevel.Information, "late");
        runtime.Dispose();

        Assert.True(runtime.Withheld.Failures >= 1);
    }

    [Fact]
    public void A_tag_whose_text_throws_drops_the_span_instead_of_throwing_at_its_end()
    {
        using var h = new Harness(export: true);
        var span = h.Source.StartActivity("work");
        _ = span!.SetTag("odd", new ThrowsOnToString());

        span.Dispose();
        h.Runtime.Flush();

        Assert.Empty(h.Spans!.Exported);
        Assert.Equal(1, h.Runtime.Withheld.Failures);
    }

    [Fact]
    public void A_field_whose_text_throws_drops_the_line_and_counts_it()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("odd {Value}", ("Value", new ThrowsOnToString()));

        Assert.Equal(1, h.Runtime.Withheld.Failures);
        Assert.Empty(h.Sink.Lines);
    }

    private sealed class ThrowingSink : ILogSink
    {
        public void WriteLine(string line) => throw new IOException("disk full");

        public void Flush() => throw new IOException("disk full");
    }

    private sealed class ThrowsOnToString
    {
        public override string ToString() => throw new InvalidOperationException("broken ToString");
    }
}
