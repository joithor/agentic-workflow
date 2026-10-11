using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Testing;

/// <summary>
/// The fake for <see cref="IJudge"/>: answers from a queue (resolved by default) about the input it was asked, and keeps
/// every question. An answer queued with an empty input hash names the question's hash, as judge does.
/// </summary>
public sealed class ScriptedJudge : IJudge
{
    private readonly Queue<Result<JudgeAnswer>> _answers = new();

    /// <summary>Gets every input asked, in order.</summary>
    public IList<ResolutionCheckInput> Asked { get; } = [];

    /// <summary>Queues the next answer.</summary>
    /// <param name="answer">The answer, or an error.</param>
    /// <returns>This judge.</returns>
    public ScriptedJudge Answers(Result<JudgeAnswer> answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    /// <inheritdoc/>
    public Result<JudgeAnswer> ResolutionCheck(JudgeQuestion question)
    {
        Asked.Add(question.Input);
        var answer = _answers.Count > 0 ? _answers.Dequeue() : A.Verdict(JudgeDecision.Resolved);
        return answer.Map(a => a.InputSha256.Length == 0 ? a with { InputSha256 = question.InputSha256 } : a);
    }

    /// <summary>The error the port returns when judge gives no usable verdict.</summary>
    /// <returns>AW-BUGFIX-016.</returns>
    public static Error Fails() => BugFixErrors.JudgeUnavailable.With("judge failed (exit 1): provider chain exhausted");
}
