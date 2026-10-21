namespace Aw.CodeIndex.Core.Similarity;

/// <summary>The 64-bit FNV-1a hash, spelled out so a signature is the same on every runtime (string.GetHashCode is seeded per process).</summary>
internal static class Fnv1a
{
    internal static ulong Hash(string text) => throw new NotSupportedException();
}
