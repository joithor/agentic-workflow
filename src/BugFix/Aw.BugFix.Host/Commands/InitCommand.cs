using System.Text.Json;
using Aw.BugFix.Features.Init;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'init' command: binds its arguments, calls Init and maps the result to an exit code.</summary>
internal static class InitCommand
{
    internal const string Name = "init";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 4 || args[0] != "--state" || args[2] != "--ticket")
        {
            return CommandOutput.Usage("init --state <dir> --ticket <ticket.json>");
        }

        var service = services.GetRequiredService<IService<InitRequest, InitResponse>>();
        var result = await service.Handle(new InitRequest(args[1], args[3]), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
