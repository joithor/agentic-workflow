namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The missing-CLI guard of 'session wrap' (launcher spec §3): whether the agent's executable resolves.</summary>
internal static class BinOnPath
{
    /// <summary>Whether <paramref name="bin"/> is an executable file, by path or on <paramref name="path"/>.</summary>
    /// <param name="bin">The first element of the agent's command line.</param>
    /// <param name="path">The PATH.</param>
    /// <returns>True when it resolves.</returns>
    internal static bool Resolves(string bin, string? path)
        => throw new NotSupportedException("BinOnPath: make BinOnPathTests pass");
}
