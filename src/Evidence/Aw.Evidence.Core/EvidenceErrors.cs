using Aw.Kernel;

namespace Aw.Evidence.Core;

/// <summary>The Evidence error catalogue (codes AW-EVIDENCE-NNN).</summary>
public static class EvidenceErrors
{
    /// <summary>AW-EVIDENCE-001: input failed validation.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-EVIDENCE-001", ErrorKind.Invalid, "Evidence input failed validation");

    /// <summary>AW-EVIDENCE-002: a UI script is malformed or breaks a script rule.</summary>
    public static readonly ErrorDefinition ScriptInvalid = new("AW-EVIDENCE-002", ErrorKind.Invalid, "UI script is invalid");

    /// <summary>AW-EVIDENCE-003: a design manifest is malformed, has an unknown key or breaks a manifest rule.</summary>
    public static readonly ErrorDefinition ManifestInvalid = new("AW-EVIDENCE-003", ErrorKind.Invalid, "Design manifest is invalid");

    /// <summary>AW-EVIDENCE-004: the --host value is refused (not local, not a guarded preview host, or not allowed).</summary>
    public static readonly ErrorDefinition HostRefused = new("AW-EVIDENCE-004", ErrorKind.Forbidden, "Host refused");

    /// <summary>AW-EVIDENCE-005: a step leaves the host, or looks like a write without --allow-writes.</summary>
    public static readonly ErrorDefinition StepsRefused = new("AW-EVIDENCE-005", ErrorKind.Forbidden, "Steps refused");

    /// <summary>AW-EVIDENCE-006: sign-in credentials are missing from the environment (names, never values).</summary>
    public static readonly ErrorDefinition CredentialsMissing = new("AW-EVIDENCE-006", ErrorKind.Invalid, "Credentials missing");

    /// <summary>AW-EVIDENCE-007: a region is not wholly inside an image or the viewport.</summary>
    public static readonly ErrorDefinition RegionOutside = new("AW-EVIDENCE-007", ErrorKind.Invalid, "Region outside the image");

    /// <summary>AW-EVIDENCE-008: two images that must be compared have different sizes.</summary>
    public static readonly ErrorDefinition SizeMismatch = new("AW-EVIDENCE-008", ErrorKind.Invalid, "Image sizes differ");

    /// <summary>AW-EVIDENCE-009: a machine lock stayed busy past the timeout.</summary>
    public static readonly ErrorDefinition LockBusy = new("AW-EVIDENCE-009", ErrorKind.Timeout, "Lock busy");

    /// <summary>AW-EVIDENCE-010: a run file does not exist.</summary>
    public static readonly ErrorDefinition FileNotFound = new("AW-EVIDENCE-010", ErrorKind.NotFound, "File not found");

    /// <summary>AW-EVIDENCE-011: a run file can't be read, written or deleted.</summary>
    public static readonly ErrorDefinition FileUnavailable = new("AW-EVIDENCE-011", ErrorKind.Unavailable, "File unavailable");

    /// <summary>AW-EVIDENCE-012: bytes are not an image the codec reads.</summary>
    public static readonly ErrorDefinition ImageInvalid = new("AW-EVIDENCE-012", ErrorKind.Invalid, "Image is invalid");

    /// <summary>AW-EVIDENCE-013: a browser action or check failed (element missing, timeout, crash).</summary>
    public static readonly ErrorDefinition BrowserFailed = new("AW-EVIDENCE-013", ErrorKind.Unavailable, "Browser action failed");

    /// <summary>AW-EVIDENCE-014: a stack probe failed (page, database or port).</summary>
    public static readonly ErrorDefinition ProbeFailed = new("AW-EVIDENCE-014", ErrorKind.Unavailable, "Stack probe failed");

    /// <summary>Every Evidence error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        Invalid, ScriptInvalid, ManifestInvalid, HostRefused, StepsRefused, CredentialsMissing, RegionOutside,
        SizeMismatch, LockBusy, FileNotFound, FileUnavailable, ImageInvalid, BrowserFailed, ProbeFailed,
    ];
}
