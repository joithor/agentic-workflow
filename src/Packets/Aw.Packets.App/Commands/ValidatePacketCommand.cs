using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.ValidatePacket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>The 'validate' command: binds its arguments, calls ValidatePacket and maps the result to an exit code.</summary>
internal static class ValidatePacketCommand
{
    internal const string Name = "validate";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 0)
        {
            return CommandOutput.Usage("validate");
        }

        var service = services.GetRequiredService<IService<ValidatePacketRequest, ValidatePacketResponse>>();
        var result = await service.Handle(new ValidatePacketRequest(), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
