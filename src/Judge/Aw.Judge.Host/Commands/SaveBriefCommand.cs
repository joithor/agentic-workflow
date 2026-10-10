using Aw.Judge.Features.SaveBrief;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief save' &lt; payload.json: saves the brief and prints nothing (TS runBriefSave).</summary>
internal static class SaveBriefCommand
{
    internal const string Name = "save";

    internal static async Task<int> Run(IServiceProvider services)
    {
        using var input = CommandOutput.ParseInput(await CommandOutput.ReadInput(CancellationToken.None).ConfigureAwait(false));
        if (input is null)
        {
            return CommandOutput.Emit(1, "", "input is not valid JSON");
        }

        if (BriefPayload.Read(input.RootElement) is not { } request)
        {
            return CommandOutput.Emit(1, "", "invalid brief payload");
        }

        var service = services.GetRequiredService<IService<SaveBriefRequest, SaveBriefResponse>>();
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Emit(1, "", result.Error.Message) : CommandOutput.Emit(0, "");
    }
}
