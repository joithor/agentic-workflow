using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Updates with a validator (engine spec rev 8 §5.2): 'bad' is rejected by the validator; each accepted answer counts.</summary>
[Workflow("UpdateProbe")]
internal sealed class UpdateProbeWorkflow
{
    private bool _finished;

    /// <summary>Accepted answers.</summary>
    [WorkflowQuery]
    public int Answers { get; private set; }

    /// <summary>Waits until finished.</summary>
    /// <returns>The accepted answers.</returns>
    [WorkflowRun]
    public async Task<int> RunAsync()
    {
        await Wf.WaitConditionAsync(() => _finished);
        return Answers;
    }

    /// <summary>Counts an answer.</summary>
    /// <param name="text">The answer.</param>
    /// <returns>The count after it.</returns>
    [WorkflowUpdate]
    public Task<int> AnswerAsync(string text)
    {
        _ = text;
        Answers++;
        return Task.FromResult(Answers);
    }

    /// <summary>Rejects 'bad' without touching state or history.</summary>
    /// <param name="text">The answer.</param>
    [WorkflowUpdateValidator(nameof(AnswerAsync))]
    public void ValidateAnswer(string text)
    {
        if (text == "bad")
        {
            throw new ArgumentException("SND-WF-003: 'bad' is not an allowed answer");
        }
    }

    /// <summary>Ends the run.</summary>
    /// <returns>Done.</returns>
    [WorkflowSignal]
    public Task FinishAsync()
    {
        _finished = true;
        return Task.CompletedTask;
    }
}
