namespace Aw.Packets.Core.Model;

/// <summary>The one rule for paths a packet names (links and 'new:' scope entries): relative, '/' separators, no '.' or '..' segments.</summary>
public static class RelativePath
{
    /// <summary>True when <paramref name="path"/> is a safe relative path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>Whether it is safe.</returns>
    public static bool IsSafe(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Length > 0
            && !path.Contains('\\', StringComparison.Ordinal)
            && !path.Contains(':', StringComparison.Ordinal)
            && !path.StartsWith('/')
            && path.Split('/').All(static s => s.Length > 0 && s is not ("." or ".."));
    }
}
