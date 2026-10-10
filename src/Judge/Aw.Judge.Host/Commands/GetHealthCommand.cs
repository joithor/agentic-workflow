using System.Text.Json;
using Aw.Judge.Features.GetHealth;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>The 'health' command: binds its arguments, calls GetHealth and maps the result to an exit code.</summary>
internal static class GetHealthCommand
{
    internal const string Name = "health";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 0)
        {
            return CommandOutput.Usage("health");
        }

        var service = services.GetRequiredService<IService<GetHealthRequest, GetHealthResponse>>();
        var result = await service.Handle(new GetHealthRequest(), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
