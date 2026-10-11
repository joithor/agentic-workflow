using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RunUi;

internal sealed class RunUiService(IStateStore store, IRunLedger ledger, WorkTree tree, CheckLocator locator, IUiEvidence ui, TimeProvider clock)
    : IService<RunUiRequest, RunUiResponse>
{
    public ValueTask<Result<RunUiResponse>> Handle(RunUiRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Run(request, state)));

    private Result<RunUiResponse> Run(RunUiRequest request, BugFixState state)
    {
        var checkout = tree.Clean(state, request.WorkingDirectory, "commit before running the check so the result maps to a commit");
        var script = checkout.Bind(_ => locator.Locate(request.WorkingDirectory, request.Check));
        if (script.IsError)
        {
            return script.Error;
        }

        var (commit, directory) = (checkout.Value.Head, checkout.Value.Directory);
        // The served app must be rebuilt from this checkout; the helper can label the run, not check what the server runs.
        var reserved = ledger.Reserve(request.StateDirectory, clock.GetUtcNow(), CheckKind.Ui);
        if (reserved.IsError)
        {
            return reserved.Error;
        }

        var slot = reserved.Value;
        var run = ui.Run(new UiRunRequest(script.Value.FullPath, slot.Output, commit, request.WorkingDirectory, slot.Log));
        if (run.IsError)
        {
            ledger.Release(slot);
            return run.Error;
        }

        var record = new RunRecord(CheckKind.Ui, run.Value.Outcome, commit, directory, run.Value.ScriptSha256, Command: null, ProgramSha256: string.Empty, Environment: new Dictionary<string, string>(StringComparer.Ordinal), run.Value.ExitCode, slot.Output, Leftovers: new Dictionary<string, string>(StringComparer.Ordinal), checkout.Value.Root);
        var recordSha = ledger.Write(slot, record);
        return store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Map(fresh =>
        {
            store.Save(request.StateDirectory, fresh.Register(new RegisteredRun(slot.Record, recordSha)));
            return new RunUiResponse(slot.Record, record.Outcome, record.ExitCode, commit);
        }));
    }
}
