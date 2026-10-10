using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.UndoDecision;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge undo &lt;id&gt;': the decision row after undoing it; an unknown id exits 1 with no output at all (TS runUndo).</summary>
internal static class UndoDecisionCommand
{
    internal const string Name = "undo";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (!DecisionId.TryFrom(args.Count > 0 ? args[0] : "", out var id))
        {
            return CommandOutput.Emit(1, "");
        }

        var service = services.GetRequiredService<IService<UndoDecisionRequest, UndoDecisionResponse>>();
        var result = await service.Handle(new UndoDecisionRequest(id), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Emit(1, "", result.Error.Kind == ErrorKind.NotFound ? "" : result.Error.Message);
        }

        return CommandOutput.Emit(0, CommandOutput.Json(DecisionOutput.From(result.Value.Decision), json));
    }
}
