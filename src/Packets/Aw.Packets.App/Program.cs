using System.Text.Json;
using Aw.Packets.App;
using Aw.Packets.App.Commands;
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), Environment.GetEnvironmentVariable, TimeProvider.System);
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
var json = provider.GetRequiredService<JsonSerializerOptions>();   // the one Kernel JSON profile Composition registered
await using (provider.ConfigureAwait(false))
{
    return await CommandTable.Run(args, provider, json).ConfigureAwait(false);
}
