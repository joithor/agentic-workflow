using Aw.Kernel.Telemetry.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class RegistrationTests
{
    [Fact]
    public void AddKernelTelemetry_registers_loggers_that_write_to_the_sink()
    {
        var sink = new InMemoryLogSink();
        var services = new ServiceCollection().AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = sink, SourcePattern = "Aw.Test.Registration" });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        provider.GetRequiredService<ILogger<RegistrationTests>>().Emit(LogLevel.Information, "hello");
        provider.GetRequiredService<TelemetryRuntime>().Flush();

        Assert.Contains("\"message\":\"hello\"", Assert.Single(sink.Lines), StringComparison.Ordinal);
        Assert.Contains("\"category\":\"Aw.Kernel.Telemetry.Tests.RegistrationTests\"", sink.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_runtime_is_one_singleton_shared_by_the_logger_factory()
    {
        var services = new ServiceCollection().AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Registration" });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(provider.GetRequiredService<TelemetryRuntime>(), provider.GetRequiredService<TelemetryRuntime>());
        Assert.Same(provider.GetRequiredService<TelemetryRuntime>().LoggerFactory, provider.GetRequiredService<ILoggerFactory>());
    }

    [Fact]
    public void AddKernelTelemetry_starts_telemetry_in_Compose_so_spans_are_recorded_before_any_logger_is_resolved()
    {
        var sink = new InMemoryLogSink();
        using var source = new System.Diagnostics.ActivitySource("Aw.Test.Eager");
        var services = new ServiceCollection().AddKernelTelemetry(
            TelemetryOptions.FromEnvironment("aw-demo", _ => "loud", TimeProvider.System) with { Sink = sink, SourcePattern = "Aw.Test.Eager" });

        using (var span = source.StartActivity("work"))
        {
            Assert.NotNull(span);
        }

        Assert.Contains("AW-TELEMETRY-001", Assert.Single(sink.Lines), StringComparison.Ordinal);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<TelemetryRuntime>());
    }

    [Fact]
    public void AddKernelTelemetry_twice_is_refused()
    {
        var services = new ServiceCollection().AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Twice" });

        _ = Assert.Throws<InvalidOperationException>(() => services.AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", SourcePattern = "Aw.Test.Twice" }));
    }

    [Fact]
    public void Disposing_the_container_after_resolving_the_runtime_stops_telemetry()
    {
        using var source = new System.Diagnostics.ActivitySource("Aw.Test.Owned");
        var services = new ServiceCollection().AddKernelTelemetry(new TelemetryOptions { ServiceName = "aw-demo", Sink = new InMemoryLogSink(), SourcePattern = "Aw.Test.Owned" });
        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TelemetryRuntime>();

        provider.Dispose();

        Assert.False(source.HasListeners());
    }
}
