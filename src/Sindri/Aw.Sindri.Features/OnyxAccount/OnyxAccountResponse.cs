using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// What the account stage ended with. JSON: `{ url, keyPointer, profileChanged, steps: [{ step, outcome }] }`, `outcome` one of `ran`, `skipped`, `needs-human` (no terminal to ask at).
/// </summary>
/// <param name="Url">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="KeyPointer">The pointer to the stored key (never the key); null while the key step waits for the human.</param>
/// <param name="ProfileChanged">The profile file was written in this run, so it needs the human's approval.</param>
/// <param name="Steps">What each step did: signup, key, profile.</param>
public sealed record OnyxAccountResponse(string Url, string? KeyPointer, bool ProfileChanged, IReadOnlyList<OnyxStepResult> Steps)
{
    /// <summary>Gets a value indicating whether a step is waiting for the human.</summary>
    public bool NeedsHuman => Steps.Any(static s => s.Outcome == OnyxStepOutcome.NeedsHuman);
}
