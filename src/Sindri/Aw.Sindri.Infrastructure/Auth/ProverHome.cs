namespace Aw.Sindri.Infrastructure.Auth;

/// <summary>
/// Whether a home may honour the prover label (API spec §6, plan G4): its real path is inside the temp directory or
/// inside a proof folder, '&lt;state root&gt;/&lt;repo slug&gt;/proof/…'. Pure: the paths are already resolved
/// (<see cref="RealPath"/>), so the owner's real home under ~/.agentic-workflow/sindri-app never qualifies.
/// </summary>
internal static class ProverHome
{
    /// <summary>True when <paramref name="home"/> lies inside <paramref name="temp"/> or a proof folder under <paramref name="stateRoot"/>.</summary>
    internal static bool Allows(string home, string temp, string stateRoot)
    {
        if (Below(home, temp) is { Length: > 0 })
        {
            return true;
        }

        var inState = Below(home, stateRoot);
        return inState is { Length: >= 3 } && string.Equals(inState[1], "proof", StringComparison.Ordinal);
    }

    /// <summary>The segments of <paramref name="path"/> below <paramref name="root"/>, or null when it isn't below it.</summary>
    private static string[]? Below(string path, string root)
    {
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal)
            ? path[prefix.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            : null;
    }
}
