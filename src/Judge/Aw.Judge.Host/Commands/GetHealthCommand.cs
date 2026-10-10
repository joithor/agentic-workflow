using Aw.Judge.Core.Json;
using Aw.Judge.Features.GetHealth;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>The 'health' command: {"status":"ok|degraded|down","failures24h":N}, exit 0; extra arguments are ignored, as in TS.</summary>
internal static class GetHealthCommand
{
    internal const string Name = "health";

    internal static async Task<int> Run(IServiceProvider services)
    {
        var service = services.GetRequiredService<IService<GetHealthRequest, GetHealthResponse>>();
        var result = await service.Handle(new GetHealthRequest(), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Emit(1, "", $"judge: {result.Error.Message}");
        }

        var health = new JsObject()
            .Set("status", JsValue.From(Frozen.Text(result.Value.Status)))
            .Set("failures24h", JsValue.From(result.Value.Failures24h));
        return CommandOutput.Emit(0, JsJson.Stringify(health));
    }
}
