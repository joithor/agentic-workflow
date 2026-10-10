using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class OptionsTests
{
    [Fact]
    public void Defaults_export_nothing_and_log_information_and_above()
    {
        var options = new TelemetryOptions { ServiceName = "aw-demo" };

        Assert.Null(options.TraceExporter);
        Assert.Null(options.MetricExporter);
        Assert.Equal(LogLevel.Information, options.MinimumLevel);
        Assert.Equal("Aw.*", options.SourcePattern);
    }

    [Theory]
    [InlineData(null, LogLevel.Information)]
    [InlineData("", LogLevel.Information)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("none", LogLevel.None)]
    public void The_level_comes_from_AW_LOG_LEVEL(string? value, LogLevel expected)
    {
        var clock = new FakeTimeProvider();
        var options = TelemetryOptions.FromEnvironment("aw-demo", n => n == TelemetryOptions.LevelVariable ? value : null, clock);

        Assert.Equal(expected, options.MinimumLevel);
        Assert.Equal("aw-demo", options.ServiceName);
        Assert.Same(clock, options.Clock);
        Assert.Null(options.Problem);
    }

    [Theory]
    [InlineData("loud")]
    [InlineData("3")]
    public void An_unknown_level_falls_back_to_information_and_records_the_problem_without_the_value(string value)
    {
        var options = TelemetryOptions.FromEnvironment("aw-demo", _ => value, TimeProvider.System);

        Assert.Equal(LogLevel.Information, options.MinimumLevel);
        Assert.NotNull(options.Problem);
        Assert.Equal("AW-TELEMETRY-001", options.Problem.Code);
        Assert.Contains(TelemetryOptions.LevelVariable, options.Problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value, options.Problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_runtime_writes_the_problem_as_its_first_line()
    {
        var sink = new Testing.InMemoryLogSink();
        // Its own source pattern: the default 'Aw.*' would listen to every parallel test's 'Aw.Test.*' source while this
        // runtime is alive (RegistrationTests asserts a disposed runtime leaves its source with no listeners).
        var options = TelemetryOptions.FromEnvironment("aw-demo", _ => "s3cr3t-pasted-here", TimeProvider.System) with { Sink = sink, SourcePattern = "Aw.Test.Problem" };

        using (TelemetryRuntime.Start(options))
        {
        }

        var line = System.Text.Json.JsonDocument.Parse(Assert.Single(sink.Lines)).RootElement;
        Assert.Equal("warning", line.GetProperty("level").GetString());
        Assert.Equal("AW-TELEMETRY-001", line.GetProperty("eventName").GetString());
        Assert.DoesNotContain("s3cr3t", sink.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_error_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(TelemetryErrors.All).IsSuccess);
}
