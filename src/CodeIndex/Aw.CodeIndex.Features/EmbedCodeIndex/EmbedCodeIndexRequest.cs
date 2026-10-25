using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Features.EmbedCodeIndex;

/// <summary>Embeds one repo's pending symbols, without the heavy slot, until none remain or the model fails.</summary>
/// <param name="Repo">The repo and its main checkout, with its effective index settings.</param>
/// <param name="Progress">Told a counts-only line per batch ('embeddings 1,400 of 3,100').</param>
public sealed record EmbedCodeIndexRequest(CodeIndexRepo Repo, IProgress<string> Progress);
