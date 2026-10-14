using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Auth;

/// <summary>
/// The per-start human token (API spec §6): 'serve' mints it after taking the lease; the CLI and the app send it as
/// X-Sindri-Token with X-Sindri-Caller: human. A missing or stale token is SND-AUTH-001, never a silent downgrade.
/// </summary>
/// <param name="Value">The token text.</param>
public sealed record HumanToken(string Value)
{
    /// <summary>Whether <paramref name="presented"/> is this token, compared in constant time.</summary>
    /// <param name="presented">The X-Sindri-Token value, or null when the header is missing.</param>
    /// <returns>True on an exact match.</returns>
    public bool Matches(string? presented) =>
        presented is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Value), Encoding.UTF8.GetBytes(presented));

    /// <summary>Never the value: a token must not reach a log line.</summary>
    /// <returns>A placeholder.</returns>
    public override string ToString() => "HumanToken(withheld)";
}
