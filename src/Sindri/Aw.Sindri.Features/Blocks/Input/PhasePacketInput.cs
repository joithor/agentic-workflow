using Aw.Kernel;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>'input.phase-packet' (PB spec §10.1): the phase packet's phase.json and design.md as the section 'phase-packet'.</summary>
/// <param name="files">Reads the packet's files.</param>
/// <param name="checkouts">Where the repo is checked out (a relative packet directory is read from it).</param>
/// <param name="contents">Reads the stored 'fact:phasePacket'.</param>
internal sealed class PhasePacketInput(IBriefFiles files, IRepoCheckouts checkouts, IFactContents contents) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.phase-packet";

    /// <summary>The section the text is under.</summary>
    public const string Section = "phase-packet";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrInputs.PhasePacket;

    /// <inheritdoc/>
    public ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
