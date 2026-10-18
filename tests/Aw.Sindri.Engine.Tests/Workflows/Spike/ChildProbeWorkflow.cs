using Temporalio.Workflows;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>A child that returns its input with a suffix.</summary>
[Workflow("ChildProbe")]
internal sealed class ChildProbeWorkflow
{
    /// <summary>What the parent passed.</summary>
    [WorkflowQuery]
    public string? Input { get; private set; }

    /// <summary>Returns '<paramref name="input"/>:child'.</summary>
    /// <param name="input">The input.</param>
    /// <returns>The result.</returns>
    [WorkflowRun]
    public Task<string> RunAsync(string input)
    {
        Input = input;
        return Task.FromResult(input + ":child");
    }
}
