namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>The body of POST /api/embed.</summary>
/// <param name="Model">The model name.</param>
/// <param name="Input">The texts.</param>
internal sealed record EmbedRequest(string Model, IReadOnlyList<string> Input);
