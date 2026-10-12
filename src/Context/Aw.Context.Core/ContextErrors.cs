using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>
/// The context engine's error catalogue (spec §8.6), codes SND-CONTEXT-NNN. Stage S-1A owns 001–019 and defines 020
/// (the session gate, spec §3); later stages add theirs in their ranges. HTTP status follows the kind; the CLI exit
/// code is 1 for attention (003, 005, 006, 020) and 2 for a tool error.
/// </summary>
public static class ContextErrors
{
    /// <summary>SND-CONTEXT-001: the profile's 'context:' section is invalid; the message names the key path.</summary>
    public static readonly ErrorDefinition SettingsInvalid = new("SND-CONTEXT-001", ErrorKind.Invalid, "The profile's context section is invalid");

    /// <summary>SND-CONTEXT-002: a source the profile names isn't configured, or its secret pointer doesn't resolve.</summary>
    public static readonly ErrorDefinition SourceNotConfigured = new("SND-CONTEXT-002", ErrorKind.Invalid, "A context source isn't configured");

    /// <summary>SND-CONTEXT-003: an unknown anchor, or a record outside a session's repo scope (answered the same way).</summary>
    public static readonly ErrorDefinition UnknownAnchor = new("SND-CONTEXT-003", ErrorKind.NotFound, "Unknown anchor");

    /// <summary>SND-CONTEXT-004: the store can't open, or its schema doesn't match this build.</summary>
    public static readonly ErrorDefinition StoreUnavailable = new("SND-CONTEXT-004", ErrorKind.Unavailable, "The context store can't be opened");

    /// <summary>SND-CONTEXT-005: an output path inside a git worktree.</summary>
    public static readonly ErrorDefinition PathInWorktree = new("SND-CONTEXT-005", ErrorKind.Invalid, "Output path inside a git worktree");

    /// <summary>SND-CONTEXT-006: the question itself is invalid (empty, two shapes, a value out of range).</summary>
    public static readonly ErrorDefinition QueryInvalid = new("SND-CONTEXT-006", ErrorKind.Invalid, "The context query is invalid");

    /// <summary>SND-CONTEXT-010: one source is unavailable; the query goes on without it (degraded).</summary>
    public static readonly ErrorDefinition SourceUnavailable = new("SND-CONTEXT-010", ErrorKind.Unavailable, "A context source is unavailable");

    /// <summary>SND-CONTEXT-011: every source is unavailable (a tool error, not "no results").</summary>
    public static readonly ErrorDefinition AllSourcesUnavailable = new("SND-CONTEXT-011", ErrorKind.Unavailable, "Every context source is unavailable");

    /// <summary>SND-CONTEXT-020: session reads are off ('context.sessions.enabled: false'), on every transport.</summary>
    public static readonly ErrorDefinition SessionsDisabled = new("SND-CONTEXT-020", ErrorKind.Forbidden, "Context reads from sessions are off");

    /// <summary>SND-CONTEXT-021: an MCP call without X-Sindri-Repo, so it has no repo scope (stage S-1M).</summary>
    public static readonly ErrorDefinition McpRepoMissing = new("SND-CONTEXT-021", ErrorKind.Invalid, "The MCP call names no repo");

    /// <summary>SND-CONTEXT-022: an MCP tool's arguments don't match its input schema (S-1M).</summary>
    public static readonly ErrorDefinition McpArgumentsInvalid = new("SND-CONTEXT-022", ErrorKind.Invalid, "The MCP tool's arguments are invalid");

    /// <summary>SND-CONTEXT-023: an MCP tool Sindri doesn't have (S-1M).</summary>
    public static readonly ErrorDefinition McpToolUnknown = new("SND-CONTEXT-023", ErrorKind.NotFound, "Unknown MCP tool");

    /// <summary>SND-CONTEXT-024: 'sindri mcp register' outside every profile repo, or with a repo the profile doesn't name (S-1M).</summary>
    public static readonly ErrorDefinition McpRegisterRepoUnknown = new("SND-CONTEXT-024", ErrorKind.Invalid, "Not a repo the profile names");

    /// <summary>SND-CONTEXT-025: 'sindri mcp register --apply' couldn't run the claude CLI, or it failed (S-1M).</summary>
    public static readonly ErrorDefinition McpRegisterFailed = new("SND-CONTEXT-025", ErrorKind.Unavailable, "The claude CLI couldn't register the MCP server");

    /// <summary>SND-CONTEXT-026: a user-wide 'sindri' MCP entry in a provider's config (doctor context:mcp-scope; S-1M).</summary>
    public static readonly ErrorDefinition McpUserWide = new("SND-CONTEXT-026", ErrorKind.Conflict, "A user-wide sindri MCP entry");

    /// <summary>Every Context error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [SettingsInvalid, SourceNotConfigured, UnknownAnchor, StoreUnavailable, PathInWorktree, QueryInvalid, SourceUnavailable, AllSourcesUnavailable, SessionsDisabled,
         McpRepoMissing, McpArgumentsInvalid, McpToolUnknown, McpRegisterRepoUnknown, McpRegisterFailed, McpUserWide];
}
