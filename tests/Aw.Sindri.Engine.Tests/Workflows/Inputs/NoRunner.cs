using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>An input runner for tests whose texts are all kept: running an input again is a failure of the test.</summary>
internal sealed class NoRunner : IStepInputRunner
{
    public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("a kept text must not run the input again");
}
