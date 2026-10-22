namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>The answer of GET /api/tags; only the installed models are read.</summary>
/// <param name="Models">The installed models.</param>
internal sealed record TagsResponse(IReadOnlyList<InstalledModel>? Models);
