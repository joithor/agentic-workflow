using System.Collections;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The rows of a folder, built when the list asks for them: a folder of tens of thousands of entries makes a row view model
/// only for the ones scrolled to. The entries are the page-source seam: today the whole served folder, later (when the
/// filemap serves cursor pages) a list that fills pages as the indexer reaches them. A filter is a list of positions into the entries, so narrowing copies no entry.
/// </summary>
internal sealed class LazyFileRows : IReadOnlyList<FileRowViewModel>
{
    private readonly IReadOnlyList<FileMapEntry> _entries;
    private readonly int[]? _positions;
    private readonly SymbolsColumnViewModel? _symbols;
    private readonly FileRowViewModel?[] _built;

    /// <summary>Rows over entries; with positions, only those entries in that order.</summary>
    /// <param name="entries">The entries as served.</param>
    /// <param name="symbols">The folder's symbols column, or null.</param>
    /// <param name="positions">Which entries to show, or null for all.</param>
    internal LazyFileRows(IReadOnlyList<FileMapEntry> entries, SymbolsColumnViewModel? symbols, int[]? positions = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        _symbols = symbols;
        _positions = positions;
        Count = positions?.Length ?? entries.Count;
        _built = new FileRowViewModel?[Count];
    }

    /// <summary>How many rows the list holds.</summary>
    public int Count { get; }

    /// <summary>How many row view models exist so far.</summary>
    internal int Built => _built.Count(static r => r is not null);

    /// <summary>The row at a position, built on first use.</summary>
    /// <param name="index">The position.</param>
    public FileRowViewModel this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return _built[index] ??= new FileRowViewModel(_entries[_positions?[index] ?? index], _symbols);
        }
    }

    /// <summary>The same entries narrowed to the names that contain some text.</summary>
    /// <param name="text">The text, any case; empty keeps every entry.</param>
    /// <returns>The narrowed list.</returns>
    internal LazyFileRows Narrow(string text)
    {
        if (text.Length == 0)
        {
            return new LazyFileRows(_entries, _symbols);
        }

        var kept = new List<int>();
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(i);
            }
        }

        return new LazyFileRows(_entries, _symbols, [.. kept]);
    }

    /// <inheritdoc />
    public IEnumerator<FileRowViewModel> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
