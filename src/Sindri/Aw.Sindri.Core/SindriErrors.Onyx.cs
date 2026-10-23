using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-ONYX-004: the Onyx pins are malformed, a downloaded file or image failed its pin, or a compose image has no pin.</summary>
    public static readonly ErrorDefinition OnyxPinsMalformed = new("SND-ONYX-004", ErrorKind.Invalid, "The Onyx pins are malformed or a download failed its pin");

    /// <summary>SND-ONYX-001: the Docker CLI is missing or its daemon isn't answering; the message says to install or start it.</summary>
    public static readonly ErrorDefinition OnyxDockerUnavailable = new("SND-ONYX-001", ErrorKind.Unavailable, "Docker is missing or not running");

    /// <summary>SND-ONYX-002: Compose v2 is missing.</summary>
    public static readonly ErrorDefinition OnyxComposeMissing = new("SND-ONYX-002", ErrorKind.Unavailable, "Docker Compose v2 is missing");

    /// <summary>SND-ONYX-003: not enough free disk or Docker memory; the message says how much is needed and where.</summary>
    public static readonly ErrorDefinition OnyxNotEnoughResources = new("SND-ONYX-003", ErrorKind.BudgetExceeded, "Not enough disk or Docker memory for Onyx");

    /// <summary>SND-ONYX-006: Onyx's port is taken by something not ours and no fallback port is free.</summary>
    public static readonly ErrorDefinition OnyxPortTaken = new("SND-ONYX-006", ErrorKind.Conflict, "Onyx's port is taken and no fallback port is free");

    /// <summary>SND-ONYX-005: Onyx didn't become ready in time, or answered with a version other than the pinned one.</summary>
    public static readonly ErrorDefinition OnyxNotReady = new("SND-ONYX-005", ErrorKind.Unavailable, "Onyx isn't ready or runs another version");

    /// <summary>SND-ONYX-007: the Onyx login given isn't an admin (it can't list groups or make a key).</summary>
    public static readonly ErrorDefinition OnyxNotAdmin = new("SND-ONYX-007", ErrorKind.Forbidden, "The Onyx login given isn't an admin");

    /// <summary>SND-ONYX-008: logging in, minting Sindri's key or verifying it failed.</summary>
    public static readonly ErrorDefinition OnyxKeyFailed = new("SND-ONYX-008", ErrorKind.Unavailable, "Minting or verifying Sindri's Onyx key failed");

    /// <summary>SND-ONYX-009: the key couldn't be stored (neither the OS secret store nor the owner-only file).</summary>
    public static readonly ErrorDefinition OnyxSecretNotStored = new("SND-ONYX-009", ErrorKind.Unavailable, "Writing the Onyx key to a secret store failed");

    /// <summary>SND-ONYX-010: Docker points at another machine; Sindri only runs Onyx on this one.</summary>
    public static readonly ErrorDefinition OnyxRemoteDocker = new("SND-ONYX-010", ErrorKind.Unavailable, "Docker points at another machine");

    /// <summary>SND-ONYX-011: a `docker compose` command failed; the message ends with the last lines it printed.</summary>
    public static readonly ErrorDefinition OnyxComposeFailed = new("SND-ONYX-011", ErrorKind.Unavailable, "A docker compose command failed");

    /// <summary>SND-ONYX-012: Sindri can't use its Onyx directory under the Sindri home (no permission to create or lock a file in it); the message names the directory.</summary>
    public static readonly ErrorDefinition OnyxWorkspaceUnusable = new("SND-ONYX-012", ErrorKind.Unavailable, "Sindri can't use its Onyx directory");
}
