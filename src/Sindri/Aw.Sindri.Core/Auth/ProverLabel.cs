namespace Aw.Sindri.Core.Auth;

/// <summary>
/// Whether this process honours X-Sindri-Client: prover from a human caller (API spec §6, plan G4): only when its home's
/// real path is inside the temp directory or a proof folder, so the owner's real process never does.
/// </summary>
/// <param name="Allowed">True for a temp or proof home.</param>
/// <param name="MayConfirm">True when the home also holds the marker file '.prover-scratch', which only the prover writes (setup-once spec §4 item 6): only then may the prover confirm what the app confirms.</param>
public sealed record ProverLabel(bool Allowed, bool MayConfirm = false);
