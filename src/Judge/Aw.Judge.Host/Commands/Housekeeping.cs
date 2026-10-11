using Aw.Judge.Features.PruneDecisionDetails;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The upkeep after a question, the only command that writes decision details: drop the details past their retention.
/// Best effort and after the output is written: a store that can't be used changes nothing about the command. One
/// DELETE per question over the decisions.ts index, which removes only what aged past the retention since the last
/// question (usually nothing).
/// </summary>
internal static class Housekeeping
{
    internal static async Task<int> AfterQuestion(int exitCode, IServiceProvider services)
    {
        if (exitCode == CommandOutput.UsageExit)
        {
            return exitCode;   // no question ran: a mistyped command line never touches the database
        }

        var service = services.GetRequiredService<IService<PruneDecisionDetailsRequest, PruneDecisionDetailsResponse>>();
        _ = await service.Handle(new PruneDecisionDetailsRequest(), CancellationToken.None).ConfigureAwait(false);   // fails open: the result is not the command's
        return exitCode;
    }
}
