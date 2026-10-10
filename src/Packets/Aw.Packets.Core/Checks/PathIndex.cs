using System.Collections;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// The base commit's files plus every declared new path, each split into segments once, with one bit per path for
/// what each distinct wildcard entry matches (a literal path or a 'new:' entry is one index). Memory is paths/8 bytes
/// per distinct wildcard entry and per task, and each wildcard entry is matched against the paths once however many
/// tasks use it.
/// </summary>
internal sealed class PathIndex
{
    private readonly string[][] _segments;
    private readonly HashSet<string> _files;
    private readonly HashSet<string> _declared;
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _position = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BitArray> _byEntry = new(StringComparer.Ordinal);

    internal PathIndex(IReadOnlySet<string> files, IEnumerable<string> declared)
    {
        _files = new HashSet<string>(files, StringComparer.Ordinal);
        _declared = new HashSet<string>(declared, StringComparer.Ordinal);
        Paths = [.. files.Concat(_declared).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        _segments = [.. Paths.Select(static p => p.Split('/'))];
        for (var i = 0; i < Paths.Count; i++)
        {
            _position[Paths[i]] = i;
        }

        foreach (var file in files.Concat(_declared))
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

    /// <summary>True when <paramref name="path"/> is a directory at the base commit, or one a declared new path needs.</summary>
    internal bool IsDirectory(string path) => _directories.Contains(path);

    /// <summary>The longest proper prefix of <paramref name="path"/> (at a '/') that is a file there or declared new, or null.</summary>
    internal string? FileAbove(string path)
    {
        for (var cut = path.LastIndexOf('/'); cut > 0; cut = path.LastIndexOf('/', cut - 1))
        {
            if (_files.Contains(path[..cut]) || _declared.Contains(path[..cut]))
            {
                return path[..cut];
            }
        }

        return null;
    }

    /// <summary>Sets the bits of the paths <paramref name="glob"/> matches in <paramref name="target"/>.</summary>
    internal void MatchInto(ScopeGlob glob, BitArray target)
    {
        if (glob.Literal is { } literal)
        {
            if (_position.TryGetValue(literal, out var at))
            {
                target[at] = true;
            }

            return;
        }

        _ = target.Or(Matched(glob));
    }

    /// <summary>True when <paramref name="glob"/> matches some path.</summary>
    internal bool MatchesAny(ScopeGlob glob) =>
        glob.Literal is { } literal ? _position.ContainsKey(literal) : Any(Matched(glob));

    // The paths a wildcard glob matches, one bit each; computed once per distinct entry. A literal path or a 'new:'
    // entry never gets one (MatchInto sets its single bit), so memory grows with the wildcard entries only.
    private BitArray Matched(ScopeGlob glob)
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
    private static bool Any(BitArray bits) => bits.HasAnySet();
}
