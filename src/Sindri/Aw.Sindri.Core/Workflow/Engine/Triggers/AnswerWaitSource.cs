using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// A 'workflow answer --text' on an answer wait (spec §3.4): the decision reads the answer as 'wait.satisfied' with
/// outcome 'answered' from this source, and commits it under the answer's own key.
/// </summary>
/// <param name="Answer">The answer wait's id.</param>
public sealed record AnswerWaitSource(AnswerId Answer) : WaitSource
{
    /// <inheritdoc/>
    public override string KeyPart => "answer:" + Answer.Value;
}
