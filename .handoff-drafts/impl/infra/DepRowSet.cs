using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>The rows of one manifest: one per package name (the first wins, names compare without case, as NuGet ids do), at most <see cref="CodeIndexRules.MaxDepsPerManifest"/>.</summary>
internal sealed class DepRowSet
{
    private readonly List<DepRecord> rows = [];
    private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

    internal bool Full => rows.Count >= CodeIndexRules.MaxDepsPerManifest;

    internal IReadOnlyList<DepRecord> Rows => rows;

    internal void Add(DepRecord row)
    {
        if (!Full && row.Name.Length > 0 && names.Add(row.Name))
        {
            rows.Add(row);
        }
    }
}
