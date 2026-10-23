using Aw.Kernel;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The facts a Step declares as inputs, shaped as the files its run directory gets before the session starts.</summary>
internal static class StepFactFiles
{
    /// <summary>The file name to content of each declared read fact that is set.</summary>
    /// <param name="descriptor">The Step's descriptor; null when the catalog doesn't know it (no files).</param>
    /// <param name="facts">The instance's stored facts.</param>
    /// <param name="contents">Reads the stored content.</param>
    /// <param name="scrubber">Removes secrets from the text.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The files; an error when a set fact's content can't be read.</returns>
    internal static ValueTask<Result<IReadOnlyDictionary<string, string>>> Build(BlockDescriptor? descriptor, IReadOnlyDictionary<FactKey, FactValue>? facts, IFactContents contents, ITextScrubber scrubber, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
