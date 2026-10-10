using System.Text.Json;
using Aw.Judge.Features.GetConfig;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge config get': the effective config as JSON, exit 0 (TS runConfigGet).</summary>
internal static class GetConfigCommand
{
    internal const string Name = "get";

    internal static async Task<int> Run(IServiceProvider services, JsonSerializerOptions json)
    {
        var service = services.GetRequiredService<IService<GetConfigRequest, GetConfigResponse>>();
        var result = await service.Handle(new GetConfigRequest(), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(result.Value.Config, json));
    }
}
