using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>'sindri mcp register' (context spec §3, API spec §8): the registration for the repo a directory is in.</summary>
/// <param name="Directory">The directory the CLI runs in.</param>
/// <param name="Repo">A profile repo's name, or null to resolve it from <paramref name="Directory"/>.</param>
/// <param name="Apply">True to write the registrations (a human at a terminal; the CLI checks), false to print them.</param>
/// <param name="Providers">Which providers to register; Claude Code alone when empty.</param>
public sealed record RegisterMcpRequest(string Directory, string? Repo, bool Apply, IReadOnlyList<ProviderFile>? Providers = null);
