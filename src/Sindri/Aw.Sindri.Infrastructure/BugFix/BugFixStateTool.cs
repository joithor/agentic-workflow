using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.BugFix;

/// <summary>
/// <see cref="IBugFixState"/> over the bugfix-state program (AW_BUGFIX_BIN, else 'bugfix-state' on the PATH), run through <see cref="IToolRunner"/>: the one place Sindri invokes it (stub until the green commit).
/// </summary>
/// <param name="runner">Runs bugfix-state.</param>
/// <param name="environment">Reads AW_BUGFIX_BIN.</param>
internal sealed class BugFixStateTool(IToolRunner runner, Func<string, string?> environment) : IBugFixState
{
    private readonly (IToolRunner, Func<string, string?>) _unused = (runner, environment);

    public ValueTask<BugFixAnswer<AttemptStarted>> StartAttempt(string stateDirectory, AttemptMode mode, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<BugFixAnswer<CandidateRecorded>> RecordCandidate(string stateDirectory, CandidateSource source, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<BugFixAnswer<CheckRun>> Recheck(string stateDirectory, string candidate, string worktree, FrozenCheck check, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<BugFixAnswer<ResolutionVerdict>> Judge(string stateDirectory, string candidate, CancellationToken cancellationToken) => throw new NotSupportedException();
}
