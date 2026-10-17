using System.Text.Json;
using Aw.Forge.Features.ParseForgeLink;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'parse' command: binds its arguments, calls ParseForgeLink and maps the result to an exit code.</summary>
internal static class ParseForgeLinkCommand
{
    internal const string Name = "parse";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 0)
        {
            return CommandOutput.Usage("parse");
        }

        var service = services.GetRequiredService<IService<ParseForgeLinkRequest, ParseForgeLinkResponse>>();
        var result = await service.Handle(new ParseForgeLinkRequest(), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
