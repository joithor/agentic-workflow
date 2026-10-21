using System.Text;

namespace Aw.CodeIndex.Core.Similarity;

/// <summary>The 64-bit FNV-1a hash, spelled out so a signature is the same on every runtime (string.GetHashCode is seeded per process).</summary>
internal static class Fnv1a
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    internal static ulong Hash(string text)
    {
        var hash = Offset;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ value) * Prime;
        }

        return hash;
    }
}
