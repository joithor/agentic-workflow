namespace Aw.Context.Core.Lenses;

/// <summary>
/// A repo's file listing kept sorted, so one directory's files are a contiguous range found by binary search and its direct children
/// are found by skipping over each child directory's range: a listing costs the children, not the files under them. Built once per
/// commit and shared (the listing is read-only after construction).
/// </summary>
public sealed class RepoTree
{
    private readonly string[] _files;

    /// <summary>Sorts and de-duplicates the listing.</summary>
    /// <param name="files">Every tracked file of the repo, paths from the root.</param>
    public RepoTree(IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = [.. files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>Every file, ordinal order.</summary>
    internal IReadOnlyList<string> Files => _files;

    /// <summary>The files under a directory prefix ('' or ending in '/') as a half-open range of <see cref="Files"/>.</summary>
    internal (int Start, int End) Under(string prefix)
    {
        var start = LowerBound(prefix, 0, _files.Length);
        return (start, UpperBound(prefix, start, _files.Length));
    }

    /// <summary>The direct children of a directory, directories first, then by name.</summary>
    internal List<TreeChild> Children(string prefix, (int Start, int End) range)
    {
        var children = new List<TreeChild>();
        var i = range.Start;
        while (i < range.End)
        {
            var rest = _files[i].AsSpan(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash < 0)
            {
                children.Add(new TreeChild(rest.ToString(), _files[i], IsDirectory: false, i, i + 1));
                i++;
                continue;
            }

            var directory = prefix + rest[..(slash + 1)].ToString();
            var end = UpperBound(directory, i, range.End);
            children.Add(new TreeChild(rest[..slash].ToString(), directory, IsDirectory: true, i, end));
            i = end;
        }

        children.Sort(static (a, b) => a.IsDirectory != b.IsDirectory ? (a.IsDirectory ? -1 : 1) : string.CompareOrdinal(a.Name, b.Name));
        return children;
    }

    private int LowerBound(string prefix, int low, int high)
    {
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (string.CompareOrdinal(_files[mid], prefix) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    // The first index at or after 'low' whose file no longer starts with the prefix (files sharing a prefix are contiguous).
    private int UpperBound(string prefix, int low, int high)
    {
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (_files[mid].StartsWith(prefix, StringComparison.Ordinal))
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
