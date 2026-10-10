using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging;

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
        var options = TelemetryOptions.FromEnvironment("aw-demo", n => n == TelemetryOptions.LevelVariable ? value : null);

        Assert.True(options.IsSuccess);
        Assert.Equal(expected, options.Value.MinimumLevel);
        Assert.Equal("aw-demo", options.Value.ServiceName);
    }

    [Theory]
    [InlineData("loud")]
    [InlineData("3")]
    public void An_unknown_level_is_an_invalid_setting(string value)
    {
        var options = TelemetryOptions.FromEnvironment("aw-demo", _ => value);

        Assert.True(options.IsError);
        Assert.Equal("AW-TELEMETRY-001", options.Error.Code);
        Assert.Contains(TelemetryOptions.LevelVariable, options.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_error_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(TelemetryErrors.All).IsSuccess);
}
