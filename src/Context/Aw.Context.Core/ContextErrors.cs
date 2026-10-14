using Aw.Kernel;

namespace Aw.Context.Core;

/// <summary>
/// The context engine's error catalogue (spec §8.6), codes SND-CONTEXT-NNN. Stage S-1A owns 001–019 and defines 020
/// (the session gate, spec §3); S-3a adds 030–034 (the map file and its store); S-3b adds 035–038 (the probe runner and anchors; 039 is reserved); later stages
/// add theirs in their ranges. HTTP status follows the kind; the CLI exit code is 1 for attention (<see cref="Attention"/>) and 2 for a tool error.
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

    /// <summary>SND-CONTEXT-035: the probe file is invalid (its shape, a duplicate or empty id, an empty question); the message names the key path and nothing was run.</summary>
    public static readonly ErrorDefinition ProbeFileInvalid = new("SND-CONTEXT-035", ErrorKind.Invalid, "The probe file is invalid");

    /// <summary>SND-CONTEXT-036: no probe file: the one named doesn't exist, or none was named and $AW_SINDRI_HOME/context/eval/probes.json doesn't exist.</summary>
    public static readonly ErrorDefinition NoProbeFile = new("SND-CONTEXT-036", ErrorKind.NotFound, "No probe file");

    /// <summary>SND-CONTEXT-037: a node name (label) matches several nodes; the message lists their keys.</summary>
    public static readonly ErrorDefinition AmbiguousAnchor = new("SND-CONTEXT-037", ErrorKind.Conflict, "The name matches several nodes");

    /// <summary>SND-CONTEXT-038: a probe run scored lower than the previous run of the same probe file (precision@5 or citation correctness).</summary>
    public static readonly ErrorDefinition ProbesRegressed = new("SND-CONTEXT-038", ErrorKind.Conflict, "The probes regressed");

    /// <summary>Every Context error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [SettingsInvalid, SourceNotConfigured, UnknownAnchor, StoreUnavailable, PathInWorktree, QueryInvalid, SourceUnavailable, AllSourcesUnavailable, SessionsDisabled,
         NoMap, MapFileInvalid, MapNodeUndefined, ExportOutsideHome, MapFileChanged, ProbeFileInvalid, NoProbeFile, AmbiguousAnchor, ProbesRegressed];

    /// <summary>
    /// The codes the CLI answers with exit 1 (attention, spec §8.6): an unknown or ambiguous anchor, a refused input or
    /// path, sessions off, no map, a refused map or probe file, a regression. The rest are tool errors (exit 2).
    /// </summary>
    public static IReadOnlySet<string> Attention { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        UnknownAnchor.Code, PathInWorktree.Code, QueryInvalid.Code, SessionsDisabled.Code, NoMap.Code, MapFileInvalid.Code, MapNodeUndefined.Code,
        ExportOutsideHome.Code, MapFileChanged.Code, ProbeFileInvalid.Code, NoProbeFile.Code, AmbiguousAnchor.Code, ProbesRegressed.Code,
    };
}
