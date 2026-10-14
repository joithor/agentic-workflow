using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>
/// The context engine's error catalogue (spec §8.6), codes SND-CONTEXT-NNN. Stage S-1A owns 001–019 and defines 020
/// (the session gate, spec §3); S-3a adds 030–034 (the map file and its store); S-5A adds 050–055 (Onyx, in the
/// S-5A/B range 050–069); later stages add theirs in their ranges. HTTP status follows the kind; the CLI exit code is
/// 1 for attention (003, 005, 006, 020, 030–034) and 2 for a tool error.
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

    /// <summary>SND-CONTEXT-005: a map file or an output path inside a git worktree.</summary>
    public static readonly ErrorDefinition PathInWorktree = new("SND-CONTEXT-005", ErrorKind.Invalid, "Output path inside a git worktree");

    /// <summary>SND-CONTEXT-006: the question itself is invalid (empty, two shapes, a value out of range).</summary>
    public static readonly ErrorDefinition QueryInvalid = new("SND-CONTEXT-006", ErrorKind.Invalid, "The context query is invalid");

    /// <summary>SND-CONTEXT-010: one source is unavailable; the query goes on without it (degraded).</summary>
    public static readonly ErrorDefinition SourceUnavailable = new("SND-CONTEXT-010", ErrorKind.Unavailable, "A context source is unavailable");

    /// <summary>SND-CONTEXT-011: every source is unavailable (a tool error, not "no results").</summary>
    public static readonly ErrorDefinition AllSourcesUnavailable = new("SND-CONTEXT-011", ErrorKind.Unavailable, "Every context source is unavailable");

    /// <summary>SND-CONTEXT-020: session reads are off ('context.sessions.enabled: false'), on every transport.</summary>
    public static readonly ErrorDefinition SessionsDisabled = new("SND-CONTEXT-020", ErrorKind.Forbidden, "Context reads from sessions are off");

    /// <summary>SND-CONTEXT-030: no map yet (fix: sindri context map import &lt;file&gt;).</summary>
    public static readonly ErrorDefinition NoMap = new("SND-CONTEXT-030", ErrorKind.NotFound, "No map yet");

    /// <summary>SND-CONTEXT-031: the map file is invalid (schema version, shape, an unknown kind or a disallowed edge); the message names the key path and nothing was written.</summary>
    public static readonly ErrorDefinition MapFileInvalid = new("SND-CONTEXT-031", ErrorKind.Invalid, "The map file is invalid");

    /// <summary>SND-CONTEXT-032: the map file refers to a node it doesn't define (an edge end, an alias target, a decided node); nothing was written.</summary>
    public static readonly ErrorDefinition MapNodeUndefined = new("SND-CONTEXT-032", ErrorKind.Invalid, "The map file refers to an undefined node");

    /// <summary>SND-CONTEXT-033: an export path outside $AW_SINDRI_HOME/context/exports/ (a worktree path is SND-CONTEXT-005).</summary>
    public static readonly ErrorDefinition ExportOutsideHome = new("SND-CONTEXT-033", ErrorKind.Invalid, "Export path outside the exports folder");

    /// <summary>SND-CONTEXT-034: the map file changed since its content hash was confirmed; nothing was written.</summary>
    public static readonly ErrorDefinition MapFileChanged = new("SND-CONTEXT-034", ErrorKind.Conflict, "The map file changed since its hash was confirmed");

    /// <summary>
    /// SND-CONTEXT-050: Onyx refused the key (HTTP 403): it is missing, wrong, or lacks the admin role that
    /// 'POST /api/admin/search' needs (spec §8.8). Fix: create an admin key for a dedicated service account, store it
    /// behind 'context.sources.onyx.token', and approve the profile again.
    /// </summary>
    public static readonly ErrorDefinition OnyxKeyRefused = new("SND-CONTEXT-050", ErrorKind.Forbidden, "Onyx refused the key: it is missing, wrong, or lacks the admin role the search route needs");

    /// <summary>
    /// SND-CONTEXT-051: Onyx answered with an unexpected status or shape (a route or field the pinned v4.9.0 doesn't have).
    /// The query goes on without Onyx (Unavailable). Fix: check the running release against 'context.sources.onyx.version'.
    /// </summary>
    public static readonly ErrorDefinition OnyxUnexpectedReply = new("SND-CONTEXT-051", ErrorKind.Unavailable, "Onyx answered with an unexpected shape; check the pinned version");

    /// <summary>SND-CONTEXT-052: Onyx has no document with that id (it was deleted or its connector no longer indexes it).</summary>
    public static readonly ErrorDefinition OnyxDocumentNotFound = new("SND-CONTEXT-052", ErrorKind.NotFound, "Onyx has no such document");

    /// <summary>
    /// SND-CONTEXT-053: the Onyx key is an admin key of a person's account, not of a dedicated service account (spec
    /// §8.8). Doctor shows it as degraded. Fix: create a service account for Sindri alone and use its key.
    /// </summary>
    public static readonly ErrorDefinition OnyxKeyNotServiceAccount = new("SND-CONTEXT-053", ErrorKind.Forbidden, "The Onyx key doesn't belong to a dedicated service account");

    /// <summary>
    /// SND-CONTEXT-055: Onyx isn't on a loopback address, or its version differs from the pinned one (spec §8.8). The
    /// message names the key path ('context.sources.onyx.url' or '.version') and the fix, never the token.
    /// </summary>
    public static readonly ErrorDefinition OnyxNotPinnedOrLoopback = new("SND-CONTEXT-055", ErrorKind.Invalid, "Onyx isn't on a loopback address, or its version differs from the pinned one");

    /// <summary>Every Context error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [SettingsInvalid, SourceNotConfigured, UnknownAnchor, StoreUnavailable, PathInWorktree, QueryInvalid, SourceUnavailable, AllSourcesUnavailable, SessionsDisabled,
         NoMap, MapFileInvalid, MapNodeUndefined, ExportOutsideHome, MapFileChanged,
         OnyxKeyRefused, OnyxUnexpectedReply, OnyxDocumentNotFound, OnyxKeyNotServiceAccount, OnyxNotPinnedOrLoopback];
}
