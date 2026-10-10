using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RunTest;

internal sealed class RunTestService(IStateStore store, IRunLedger ledger, IGit git, IFiles files, CheckLocator locator, ICheckRunner runner, TimeProvider clock)
    : IService<RunTestRequest, RunTestResponse>
{
    public ValueTask<Result<RunTestResponse>> Handle(RunTestRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(_ => Run(request)));

    private Result<RunTestResponse> Run(RunTestRequest request)
    {
        var check = Clean(git, request.WorkingDirectory)
            .Bind(_ => locator.Locate(request.WorkingDirectory, request.Check))
            .Bind(file => Executes(request, file));
        var sha = check.Bind(file => files.Sha256(file.FullPath));
        var commit = sha.Bind(_ => git.Head(request.WorkingDirectory));
        if (commit.IsError)
        {
            return commit.Error;
        }

        var slot = ledger.Reserve(request.StateDirectory, clock.GetUtcNow(), CheckKind.Test);
        var exitCode = runner.Run(request.Command, request.WorkingDirectory, slot.Log);
        var outcome = exitCode == 0 ? RunOutcome.Passed : RunOutcome.Failed;
        var record = new RunRecord(CheckKind.Test, outcome, commit.Value, sha.Value, request.Command, exitCode, slot.Log);
        var recordSha = ledger.Write(slot, record);

        // Re-read: the run may have taken minutes.
        return store.Load(request.StateDirectory).Map(fresh =>
        {
            store.Save(request.StateDirectory, fresh with { Runs = [.. fresh.Runs, new RegisteredRun(slot.Record, recordSha)] });
            return new RunTestResponse(slot.Record, outcome, exitCode, commit.Value);
        });
    }

    private static Result<Unit> Clean(IGit git, string directory) =>
        git.IsClean(directory).Bind<Unit>(clean => clean
            ? Unit.Value
            : BugFixErrors.DirtyTree.With($"{directory} has uncommitted or untracked changes; commit before running the check so the result maps to a commit"));

    // The command must name the check file itself as an argument (not inside a shell string), and must execute it,
    // so a baseline of `false` and a run of `true` can't stand in for the check.
    private Result<CheckFile> Executes(RunTestRequest request, CheckFile check)
    {
        var named = request.Command.Any(arg =>
            arg == check.RepoPath || files.RealPath(files.FullPath(request.WorkingDirectory, arg)) == check.FullPath);
        if (!named)
        {
            return BugFixErrors.CommandRefused.With($"the command must pass the check file ({check.RepoPath}) as an argument");
        }

        return CheckCommandRule.Refusal(request.Command) is { } reason ? BugFixErrors.CommandRefused.With(reason) : check;
    }
}
