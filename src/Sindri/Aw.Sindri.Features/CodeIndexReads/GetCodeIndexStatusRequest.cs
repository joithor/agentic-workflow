using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Each repo's index state (GET /sindri/code-index/status?repo=): from the index files only, for the caller's repos.</summary>
/// <param name="Repo">One repo, or null for every repo in the caller's scope.</param>
public sealed record GetCodeIndexStatusRequest(string? Repo = null) : IQuery<CodeIndexStatusView>;
