using Aw.Kernel;

namespace Aw.Meta.Core;

/// <summary>The Meta error catalogue (codes AW-META-NNN).</summary>
public static class MetaErrors
{
    /// <summary>AW-META-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-META-001", ErrorKind.Invalid, "Meta input failed validation");

    /// <summary>AW-META-002.</summary>
    public static readonly ErrorDefinition NoTeamConfig = new("AW-META-002", ErrorKind.NotFound, "No team config; run aw-meta team-init");

    /// <summary>AW-META-003.</summary>
    public static readonly ErrorDefinition ConfigInvalid = new("AW-META-003", ErrorKind.Invalid, "The team config is not valid");

    /// <summary>AW-META-004.</summary>
    public static readonly ErrorDefinition TranscriptUnreadable = new("AW-META-004", ErrorKind.Unavailable, "A transcript can't be read");

    /// <summary>AW-META-005.</summary>
    public static readonly ErrorDefinition MessageNotFound = new("AW-META-005", ErrorKind.NotFound, "No such message");

    /// <summary>AW-META-006.</summary>
    public static readonly ErrorDefinition QuestionNotFound = new("AW-META-006", ErrorKind.NotFound, "No such owner question");

    /// <summary>AW-META-007.</summary>
    public static readonly ErrorDefinition GateStepNotFound = new("AW-META-007", ErrorKind.NotFound, "No such gate step");

    /// <summary>AW-META-008.</summary>
    public static readonly ErrorDefinition LogUnwritable = new("AW-META-008", ErrorKind.Unavailable, "The running log can't be written");

    /// <summary>AW-META-009.</summary>
    public static readonly ErrorDefinition CommandNotStarted = new("AW-META-009", ErrorKind.Unavailable, "A command could not be started");

    /// <summary>AW-META-010.</summary>
    public static readonly ErrorDefinition HostUnreadable = new("AW-META-010", ErrorKind.Unavailable, "The process table can't be read");

    /// <summary>AW-META-011.</summary>
    public static readonly ErrorDefinition ConfigExists = new("AW-META-011", ErrorKind.Conflict, "A team config already exists");

    /// <summary>Every Meta error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [Invalid, NoTeamConfig, ConfigInvalid, TranscriptUnreadable, MessageNotFound, QuestionNotFound, GateStepNotFound, LogUnwritable, CommandNotStarted, HostUnreadable, ConfigExists];
}
