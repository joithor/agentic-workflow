using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>'input.pr.context' (PB spec §10.1): the pull request's state, changed paths, failing check excerpts and comment thread as the section 'pr-context'.</summary>
/// <param name="pulls">The forge port.</param>
/// <param name="changed">Lists the paths a change touches.</param>
/// <param name="scrubber">Takes secrets out of everything served.</param>
/// <param name="contents">Reads the stored facts.</param>
internal sealed class PrContextInput(IPullRequests pulls, IChangedPaths changed, ITextScrubber scrubber, IFactContents contents) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.pr.context";

    /// <summary>The section the text is under.</summary>
    public const string Section = "pr-context";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrInputs.Context;

    /// <inheritdoc/>
    public ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
