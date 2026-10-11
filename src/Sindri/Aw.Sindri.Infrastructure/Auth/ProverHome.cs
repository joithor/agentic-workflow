namespace Aw.Sindri.Infrastructure.Auth;

/// <summary>
/// Whether a home may honour the prover label (API spec §6, plan G4): its real path is inside the temp directory or
/// inside a proof folder, '&lt;state root&gt;/&lt;repo slug&gt;/proof/…'. Pure: the paths are already resolved.
/// </summary>
internal static class ProverHome
{
    /// <summary>True when <paramref name="home"/> lies inside <paramref name="temp"/> or a proof folder under <paramref name="stateRoot"/>.</summary>
    internal static bool Allows(string home, string temp, string stateRoot) =>
        throw new NotSupportedException("red: ProverHome.Allows");
}
