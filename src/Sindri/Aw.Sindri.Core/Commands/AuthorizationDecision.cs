using Aw.Kernel;

namespace Aw.Sindri.Core.Commands;

/// <summary>What the authorization table decided for one command attempt (API spec §4.2).</summary>
/// <param name="Caller">The caller as the audit row names it: its actor, or 'none' without a caller context.</param>
/// <param name="Refusal">The SND-AUTH-002 error when refused, or null when allowed.</param>
public sealed record AuthorizationDecision(string Caller, Error? Refusal)
{
    /// <summary>True when the caller may run the command.</summary>
    public bool Allowed => Refusal is null;
}
