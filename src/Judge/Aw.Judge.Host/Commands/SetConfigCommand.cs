using System.Text.Json;
using Aw.Judge.Features.SetConfig;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge config set &lt;question&gt; &lt;enabled|threshold&gt; &lt;value&gt;': writes config.json and prints the new config;
/// a threshold that isn't a number exits 1 'not a number: &lt;value&gt;' (TS runConfigSet).</summary>
internal static class SetConfigCommand
{
    internal const string Name = "set";

    internal static async Task<int> Run(string question, string field, string value, IServiceProvider services, JsonSerializerOptions json)
    {
        var service = services.GetRequiredService<IService<SetConfigRequest, SetConfigResponse>>();
        var result = await service.Handle(new SetConfigRequest(question, field, value), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(result.Value.Config, json));
    }
}
