namespace Aw.Sindri.Host.Mcp;

/// <summary>A refused tool call's structured content: the catalogue code and its message (which carries the fix).</summary>
/// <param name="Code">The SND code.</param>
/// <param name="Message">What happened and the fix.</param>
internal sealed record McpToolError(string Code, string Message);
