using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Testing;

/// <summary>The fake for <see cref="IUiEvidence"/>: answers from a queue (one passed step by default) and keeps every request.</summary>
/// <param name="files">The file system the script hash is read from.</param>
public sealed class ScriptedUiEvidence(InMemoryRepos files) : IUiEvidence
{
    private readonly Queue<Func<UiRunRequest, Result<UiRun>>> _answers = new();

    /// <summary>Gets every request, in order.</summary>
    public IList<UiRunRequest> Requests { get; } = [];

    /// <summary>Queues the next run's step outcomes; the script hash is the script's real hash.</summary>
    /// <param name="steps">The steps' outcomes.</param>
    /// <returns>This runner.</returns>
    public ScriptedUiEvidence Steps(params RunOutcome[] steps)
    {
        _answers.Enqueue(r => files.Sha256(r.Script).Map(sha => new UiRun(steps, sha, steps.Contains(RunOutcome.Passed) && steps.All(static s => s == RunOutcome.Passed) ? 0 : 1)));
        return this;
    }

    /// <summary>Queues an error answer.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This runner.</returns>
    public ScriptedUiEvidence Fails(Error error)
    {
        _answers.Enqueue(_ => error);
        return this;
    }

    /// <inheritdoc/>
    public Result<UiRun> Run(UiRunRequest request)
    {
        Requests.Add(request);
        return _answers.Count > 0 ? _answers.Dequeue()(request) : files.Sha256(request.Script).Map(sha => new UiRun([RunOutcome.Passed], sha, 0));
    }
}
