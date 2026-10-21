using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>The second input of a Step, to see declaration order.</summary>
/// <param name="probe">What it does and sees.</param>
internal sealed class SecondProbeInput(InputProbe probe) : IStepInput, IDescribedBlock
{
    public const string Id = "input.test.second";

    public static BlockDescriptor Descriptor { get; } = TestBlocks.StepInput(Id);

    public ValueTask<Kernel.Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<InjectionSections>>(probe.Fill(context));
}
