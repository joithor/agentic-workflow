namespace Aw.Sindri.Core.Mcp;

/// <summary>What doctor context:mcp-scope found in a provider file.</summary>
public enum McpScopeState
{
    /// <summary>A user-wide 'sindri' entry.</summary>
    UserWide,

    /// <summary>The file couldn't be read or parsed, so it couldn't be checked.</summary>
    Unreadable,
}
