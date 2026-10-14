namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>'sindri mcp register' (context spec §3, API spec §8): the registration for the repo a directory is in.</summary>
/// <param name="Directory">The directory the CLI runs in.</param>
/// <param name="Repo">A profile repo's name, or null to resolve it from <paramref name="Directory"/>.</param>
/// <param name="Apply">True to run the command (a human at a terminal; the CLI checks), false to print it.</param>
public sealed record RegisterMcpRequest(string Directory, string? Repo, bool Apply);
