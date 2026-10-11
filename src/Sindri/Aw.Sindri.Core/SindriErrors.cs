using Aw.Kernel;

namespace Aw.Sindri.Core;

/// <summary>The Sindri error catalogue (codes SND-AREA-NNN, docs/csharp/sindri.md).</summary>
public static class SindriErrors
{
    /// <summary>SND-LEASE-001: another live process holds the host lease.</summary>
    public static readonly ErrorDefinition LeaseHeld = new("SND-LEASE-001", ErrorKind.Conflict, "The host lease is held by another process");

    /// <summary>SND-LEASE-002: this process no longer holds the lease (it expired and another took it).</summary>
    public static readonly ErrorDefinition LeaseLost = new("SND-LEASE-002", ErrorKind.Conflict, "The host lease was lost");

    /// <summary>SND-LEASE-003: a write carried an epoch that isn't the current one (fencing).</summary>
    public static readonly ErrorDefinition LeaseStale = new("SND-LEASE-003", ErrorKind.Conflict, "The lease epoch is not current");

    /// <summary>SND-PROFILE-001: no profile file.</summary>
    public static readonly ErrorDefinition ProfileMissing = new("SND-PROFILE-001", ErrorKind.NotFound, "No profile found");

    /// <summary>SND-PROFILE-002: the profile doesn't parse or doesn't match the schema.</summary>
    public static readonly ErrorDefinition ProfileInvalid = new("SND-PROFILE-002", ErrorKind.Invalid, "The profile is invalid");

    /// <summary>SND-PROFILE-003: the hash to approve isn't the live profile's hash.</summary>
    public static readonly ErrorDefinition ProfileHashMismatch = new("SND-PROFILE-003", ErrorKind.Conflict, "The hash doesn't match the profile");

    /// <summary>SND-PROFILE-004: approving needs a typed confirmation at an interactive terminal.</summary>
    public static readonly ErrorDefinition ProfileNeedsTerminal = new("SND-PROFILE-004", ErrorKind.Forbidden, "Approving a profile needs an interactive terminal");

    /// <summary>SND-PROFILE-005: no profile has been approved yet.</summary>
    public static readonly ErrorDefinition ProfileNotApproved = new("SND-PROFILE-005", ErrorKind.NotFound, "No profile is approved");

    /// <summary>SND-LEDGER-001: the ledger's schema doesn't match this build.</summary>
    public static readonly ErrorDefinition LedgerSchema = new("SND-LEDGER-001", ErrorKind.Conflict, "The ledger schema doesn't match this build");

    /// <summary>SND-LEDGER-002: the ledger can't be opened or written.</summary>
    public static readonly ErrorDefinition LedgerUnavailable = new("SND-LEDGER-002", ErrorKind.Unavailable, "The ledger is unavailable");

    /// <summary>SND-API-001: the control-plane process isn't running.</summary>
    public static readonly ErrorDefinition NotRunning = new("SND-API-001", ErrorKind.Unavailable, "Sindri is not running");

    /// <summary>SND-API-002: the control-plane process didn't answer in time.</summary>
    public static readonly ErrorDefinition ApiTimeout = new("SND-API-002", ErrorKind.Timeout, "Sindri didn't answer in time");

    /// <summary>SND-CLI-001: an unknown verb or option.</summary>
    public static readonly ErrorDefinition UnknownVerb = new("SND-CLI-001", ErrorKind.Invalid, "Unknown command");

    /// <summary>SND-CLI-002: the process didn't come up after starting it.</summary>
    public static readonly ErrorDefinition StartFailed = new("SND-CLI-002", ErrorKind.Unavailable, "Sindri didn't start");

    /// <summary>SND-SCHED-001: a background service run failed.</summary>
    public static readonly ErrorDefinition ServiceFailed = new("SND-SCHED-001", ErrorKind.Unavailable, "A background service failed");

    /// <summary>Every Sindri error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        LeaseHeld, LeaseLost, LeaseStale, ProfileMissing, ProfileInvalid, ProfileHashMismatch, ProfileNeedsTerminal,
        ProfileNotApproved, LedgerSchema, LedgerUnavailable, NotRunning, ApiTimeout, UnknownVerb, StartFailed, ServiceFailed,
    ];
}
