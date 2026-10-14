namespace Aw.Sindri.Core.Mcp;

/// <summary>A provider file doctor context:mcp-scope warns about.</summary>
/// <param name="File">Which provider's file.</param>
/// <param name="Path">Its path, with '~' for the home directory.</param>
/// <param name="State">What was found.</param>
/// <param name="Problem">Why it couldn't be read, for <see cref="McpScopeState.Unreadable"/>; else null.</param>
public sealed record McpScopeFinding(ProviderFile File, string Path, McpScopeState State, string? Problem);
