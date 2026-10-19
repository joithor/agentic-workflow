namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>Spec §4.1 steps 6-9 for the Onyx that `onyx up` just brought up: the owner's sign-up in the browser, Sindri's dedicated user and token, its storage and the profile section. Idempotent and resumable; the verb behind it is the human's.</summary>
/// <param name="Port">The loopback port Onyx answers on.</param>
/// <param name="Version">The release the running Onyx reported.</param>
public sealed record OnyxAccountRequest(int Port, string Version);
