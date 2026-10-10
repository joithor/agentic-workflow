using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Readiness.App.Features.CheckRepo;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Readiness.App.Commands;

/// <summary>The 'check' command: binds its arguments, calls CheckRepo and maps the result to an exit code.</summary>
internal static class CheckRepoCommand
{
    internal const string Name = "check";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 0)
        {
            return CommandOutput.Usage("check");
        }

        var service = services.GetRequiredService<IService<CheckRepoRequest, CheckRepoResponse>>();
        var result = await service.Handle(new CheckRepoRequest(Environment.CurrentDirectory, UseHost: true), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
