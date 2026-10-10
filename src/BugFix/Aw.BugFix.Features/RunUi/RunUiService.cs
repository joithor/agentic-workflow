using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RunUi;

internal sealed class RunUiService(IStateStore store, IRunLedger ledger, IGit git, CheckLocator locator, IUiEvidence ui, TimeProvider clock)
    : IService<RunUiRequest, RunUiResponse>
{
    public ValueTask<Result<RunUiResponse>> Handle(RunUiRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(_ => Run(request)));

    private Result<RunUiResponse> Run(RunUiRequest request)
    {
        var script = git.IsClean(request.WorkingDirectory)
            .Bind<Unit>(clean => clean ? Unit.Value : BugFixErrors.DirtyTree.With(
                $"{request.WorkingDirectory} has uncommitted or untracked changes; commit before running the check so the result maps to a commit"))
            .Bind(_ => locator.Locate(request.WorkingDirectory, request.Check));
        var commit = script.Bind(_ => git.Head(request.WorkingDirectory));
        if (commit.IsError)
        {
            return commit.Error;
        }

        // The served app must be rebuilt from this checkout; the helper can label the run, not check what the server runs.
        var slot = ledger.Reserve(request.StateDirectory, clock.GetUtcNow(), CheckKind.Ui);
        var run = ui.Run(new UiRunRequest(script.Value.FullPath, slot.Output, commit.Value, request.WorkingDirectory, slot.Log));
        if (run.IsError)
        {
            return run.Error;
        }

        var record = new RunRecord(CheckKind.Ui, run.Value.Outcome, commit.Value, run.Value.ScriptSha256, Command: null, run.Value.ExitCode, slot.Output);
        var recordSha = ledger.Write(slot, record);
        return store.Load(request.StateDirectory).Map(fresh =>
        {
            store.Save(request.StateDirectory, fresh with { Runs = [.. fresh.Runs, new RegisteredRun(slot.Record, recordSha)] });
            return new RunUiResponse(slot.Record, record.Outcome, record.ExitCode, commit.Value);
        });
    }
}
