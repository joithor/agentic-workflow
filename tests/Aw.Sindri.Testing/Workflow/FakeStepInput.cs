using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>A Step input with a runtime ('input.test.echo'): fills one section, 'echo'; an argument 'fail' makes it return SND-WF-018. Test-only; no shipped catalog holds it.</summary>
public sealed class FakeStepInput : IStepInput, IDescribedBlock
{
    /// <summary>The id of the descriptor <see cref="Descriptor"/> declares.</summary>
    public const string Id = "input.test.echo";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = TestBlocks.StepInput(Id);

    /// <inheritdoc/>
    public ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        Result<InjectionSections> result = context.Arguments.ContainsKey("fail")
            ? WorkflowErrors.InputChanged.With("the fake input was told to fail")
            : new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["echo"] = "echo" });
        return ValueTask.FromResult(result);
    }
}
