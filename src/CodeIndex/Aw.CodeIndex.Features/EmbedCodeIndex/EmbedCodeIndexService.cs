using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.EmbedCodeIndex;

/// <summary>The embed slice: embeds a repo's symbols that have no vector, without the heavy slot, and carries the work on until the layer is ok.</summary>
public sealed class EmbedCodeIndexService : IService<EmbedCodeIndexRequest, EmbedCodeIndexResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<EmbedCodeIndexResponse>> Handle(EmbedCodeIndexRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("EmbedCodeIndex is not implemented yet.");
}
