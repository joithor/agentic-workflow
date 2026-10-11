using Aw.BugFix.Host;
using Aw.BugFix.Host.Commands;
using System.Text.Json;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), Environment.GetEnvironmentVariable, TimeProvider.System);
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    _ = provider.GetRequiredService<TelemetryRuntime>();   // resolved so the container disposes it on exit, which flushes
    return await CommandTable.Run(args, provider, provider.GetRequiredService<JsonSerializerOptions>()).ConfigureAwait(false);   // the one AwJson profile Compose registers
}
