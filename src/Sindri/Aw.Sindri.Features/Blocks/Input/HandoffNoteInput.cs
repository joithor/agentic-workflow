using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>'input.handoff-note' (PB spec W24): the latest 'fact:handoffNote' as the section 'handoff-note'.</summary>
/// <param name="contents">Reads the stored 'fact:handoffNote'.</param>
internal sealed class HandoffNoteInput(IFactContents contents) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.handoff-note";

    /// <summary>The section the text is under.</summary>
    public const string Section = "handoff-note";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrInputs.HandoffNote;

    /// <inheritdoc/>
    public ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
