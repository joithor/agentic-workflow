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
}
