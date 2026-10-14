using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class RequestLineTests
{
    [Fact]
    public void A_request_line_with_its_query_string_never_reaches_the_log()
    {
        using var h = new Harness(level: LogLevel.Debug);

        h.Logger(TelemetryRuntime.RequestLineCategory).Emit(LogLevel.Information, "Request starting HTTP/1.1 GET http://127.0.0.1:1/sindri/context/query?q=zebra-quokka - - -");
        h.Logger(TelemetryRuntime.RequestLineCategory).Emit(LogLevel.Warning, "a request warning stays");
        h.Logger("Aw.Demo").Emit(LogLevel.Debug, "other categories keep their level");

        var everything = h.Everything();
        Assert.DoesNotContain("zebra-quokka", everything, StringComparison.Ordinal);
        Assert.Contains("a request warning stays", everything, StringComparison.Ordinal);
        Assert.Contains("other categories keep their level", everything, StringComparison.Ordinal);
    }
}
