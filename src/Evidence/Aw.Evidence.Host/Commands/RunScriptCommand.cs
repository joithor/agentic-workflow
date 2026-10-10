using System.Text.Json;
using Aw.Evidence.Features.RunScript;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'run' command: binds its arguments, calls RunScript and maps the result to an exit code.</summary>
internal static class RunScriptCommand
{
    internal const string Name = "run";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 0)
        {
            return CommandOutput.Usage("run");
        }

        var service = services.GetRequiredService<IService<RunScriptRequest, RunScriptResponse>>();
        var result = await service.Handle(new RunScriptRequest(), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
