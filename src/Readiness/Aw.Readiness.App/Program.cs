using Aw.Kernel.Json;
using Aw.Readiness.App;
using Aw.Readiness.App.Commands;
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), Environment.GetEnvironmentVariable, TimeProvider.System);
var json = AwJson.CreateOptions(ReadinessJsonContext.Default);   // the one Kernel JSON profile
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    return await CommandTable.Run(args, provider, json).ConfigureAwait(false);
}
