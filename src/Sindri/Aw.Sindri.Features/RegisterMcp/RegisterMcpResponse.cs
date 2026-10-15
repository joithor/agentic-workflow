using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>The registration.</summary>
/// <param name="Repo">The repo it scopes to.</param>
/// <param name="Checkout">Its main checkout, where the command runs (Claude Code keys local scope by directory).</param>
/// <param name="Arguments">The claude CLI's arguments.</param>
/// <param name="Applied">True when it was run.</param>
public sealed record RegisterMcpResponse(RepoName Repo, string Checkout, IReadOnlyList<string> Arguments, bool Applied);
