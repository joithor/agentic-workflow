using System.Collections;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// The base commit's files plus every declared new path, each split into segments once, with one bit per path for
/// what each distinct write-scope entry matches. Memory is paths/8 bytes per distinct entry and per task, not a copy
/// of the matched paths, and each entry is matched against the paths once however many tasks use it.
/// </summary>
internal sealed class PathIndex
{
    private readonly string[][] _segments;
    private readonly HashSet<string> _files;
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BitArray> _byEntry = new(StringComparer.Ordinal);

    internal PathIndex(IReadOnlySet<string> files, IEnumerable<string> declared)
    {
        _files = new HashSet<string>(files, StringComparer.Ordinal);
        Paths = [.. files.Concat(declared).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        _segments = [.. Paths.Select(static p => p.Split('/'))];
        foreach (var file in files)
        {
            for (var cut = file.IndexOf('/', StringComparison.Ordinal); cut > 0; cut = file.IndexOf('/', cut + 1))
            {
                _ = _directories.Add(file[..cut]);
            }
        }
    }

    /// <summary>Every path, sorted by ordinal; a bit position is an index here.</summary>
    internal IReadOnlyList<string> Paths { get; }

    /// <summary>True when <paramref name="path"/> is a file at the base commit.</summary>
    internal bool IsFile(string path) => _files.Contains(path);

    /// <summary>True when <paramref name="path"/> is a directory at the base commit.</summary>
    internal bool IsDirectory(string path) => _directories.Contains(path);

    /// <summary>The longest proper prefix of <paramref name="path"/> (at a '/') that is a file, or null.</summary>
    internal string? FileAbove(string path)
    {
        for (var cut = path.LastIndexOf('/'); cut > 0; cut = path.LastIndexOf('/', cut - 1))
        {
            if (_files.Contains(path[..cut]))
            {
                return path[..cut];
            }
        }

        return null;
    }

    /// <summary>The paths <paramref name="glob"/> matches, one bit each; computed once per distinct entry.</summary>
    internal BitArray Matched(ScopeGlob glob)
    {
        if (_byEntry.TryGetValue(glob.Entry, out var known))
        {
            return known;
        }

        var bits = new BitArray(Paths.Count);
        for (var i = 0; i < _segments.Length; i++)
        {
            bits[i] = glob.Matches(Paths[i], _segments[i]);
        }

        _byEntry[glob.Entry] = bits;
        return bits;
    }

    /// <summary>The bits as 32-bit words, for <see cref="FirstShared"/>.</summary>
    internal static int[] Words(BitArray bits)
    {
        var words = new int[(bits.Length + 31) / 32];
        bits.CopyTo(words, 0);
        return words;
    }

    /// <summary>The first path (ordinal) set in both, or null: a word-wise AND with no allocation (it runs per task pair).</summary>
    internal string? FirstShared(int[] a, int[] b)
    {
        for (var w = 0; w < a.Length; w++)
        {
            var both = a[w] & b[w];
            if (both != 0)
            {
                return Paths[(w * 32) + System.Numerics.BitOperations.TrailingZeroCount(both)];
            }
        }

        return null;
    }

    /// <summary>True when any bit is set.</summary>
    internal static bool Any(BitArray bits) => bits.HasAnySet();
}
