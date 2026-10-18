using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>A Step input with a runtime ('input.test.echo'): fills one section, 'echo'. Test-only; no shipped catalog holds it.</summary>
public sealed class FakeStepInput : IStepInput, IDescribedBlock
{
    /// <summary>The id of the descriptor <see cref="Descriptor"/> declares.</summary>
    public const string Id = "input.test.echo";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = TestBlocks.StepInput(Id);

    /// <inheritdoc/>
    public ValueTask<InjectionSections> Fill(StepInputContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["echo"] = "echo" }));
}
