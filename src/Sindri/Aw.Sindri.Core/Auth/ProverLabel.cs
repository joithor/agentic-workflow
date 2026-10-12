namespace Aw.Sindri.Core.Auth;

/// <summary>
/// Whether this process honours X-Sindri-Client: prover from a human caller (API spec §6, plan G4): only when its home's
/// real path is inside the temp directory or a proof folder, so the owner's real process never does.
/// </summary>
/// <param name="Allowed">True for a temp or proof home.</param>
public sealed record ProverLabel(bool Allowed);
