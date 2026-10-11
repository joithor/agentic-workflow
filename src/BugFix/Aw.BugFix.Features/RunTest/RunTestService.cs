using Aw.BugFix.Core.Errors;
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
        ValueTask.FromResult(store.Load(request.StateDirectory).Bind(state => Run(request, state)));

    private Result<RunTestResponse> Run(RunTestRequest request, BugFixState state)
    {
        var tree = new TreePorts(git, files, ledger);
        var check = TreeRule.Clean(tree, state, request.WorkingDirectory, "commit before running the check so the result maps to a commit")
            .Bind(_ => locator.Locate(request.WorkingDirectory, request.Check))
            .Bind(file => Executes(request, file));
        var sha = check.Bind(file => files.Sha256(file.FullPath));
        var commit = sha.Bind(_ => git.Head(request.WorkingDirectory));
        var directory = commit.Bind(_ => git.RelativeDirectory(request.WorkingDirectory));
        if (directory.IsError)
        {
            return directory.Error;
        }

        var slot = ledger.Reserve(request.StateDirectory, clock.GetUtcNow(), CheckKind.Test);
        var exit = runner.Run(request.Command, request.WorkingDirectory, slot.Log);
        var leftovers = TreeRule.Leftovers(tree, request.WorkingDirectory).Match(static l => l, static _ => new Dictionary<string, string>(StringComparer.Ordinal));
        var record = new RunRecord(CheckKind.Test, exit.Outcome, commit.Value, directory.Value, sha.Value, request.Command, exit.ProgramSha256, exit.Environment, exit.Code, slot.Log, leftovers);
        var recordSha = ledger.Write(slot, record);

        // Registered under the lock, onto the current state: the run may have taken minutes.
        return store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Map(fresh =>
        {
            store.Save(request.StateDirectory, fresh.Register(new RegisteredRun(slot.Record, recordSha)));
            return new RunTestResponse(slot.Record, record.Outcome, exit.Code, commit.Value);
        }));
    }

    // The command must name the check file itself, by a path that resolves to it from the working directory (not inside
    // a shell string, not a same-named file elsewhere), and must execute it.
    private Result<CheckFile> Executes(RunTestRequest request, CheckFile check)
    {
        var named = request.Command.Any(arg => files.RealPath(files.FullPath(request.WorkingDirectory, arg)) == check.FullPath);
        if (!named)
        {
            return BugFixErrors.CommandRefused.With($"the command must pass the check file ({check.RepoPath}) as an argument that resolves to it from {request.WorkingDirectory}");
        }

        return CheckCommandRule.Refusal(request.Command) is { } reason ? BugFixErrors.CommandRefused.With(reason) : check;
    }
}
