namespace Aw.Sindri.Host.Mcp;

/// <summary>One page of every list in a tool answer. The same window applies to each list.</summary>
/// <param name="Limit">How many items each list keeps.</param>
/// <param name="Offset">How many items each list skips.</param>
internal sealed record McpWindow(int Limit, int Offset);
