using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Features.EmbedCodeIndex;

/// <summary>Where a repo's embeddings stand after an embed run.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Embedded">How many symbols have a vector.</param>
/// <param name="Embeddable">How many symbols should have one.</param>
/// <param name="Embeddings">The embeddings layer's state: ok when every symbol has a vector, pending while some remain.</param>
public sealed record EmbedCodeIndexResponse(RepoName Repo, int Embedded, int Embeddable, LayerState Embeddings);
