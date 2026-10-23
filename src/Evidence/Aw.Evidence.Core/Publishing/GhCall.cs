namespace Aw.Evidence.Core.Publishing;

/// <summary>One gh invocation.</summary>
/// <param name="Args">The arguments after gh (for the git data API: api, a method flag, the repo-scoped path).</param>
/// <param name="Stdin">Text written to its standard input (a JSON request body, with --input -), or null for none.</param>
public sealed record GhCall(IReadOnlyList<string> Args, string? Stdin = null);
