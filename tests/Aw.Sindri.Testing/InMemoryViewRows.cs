using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>View tables in memory: each row is its JSON text, ordered by key, as the SQLite views store keeps it.</summary>
/// <param name="tables">The tables, by view name.</param>
/// <param name="options">The package's AwJson options.</param>
internal sealed class InMemoryViewRows(Dictionary<string, SortedDictionary<string, string>> tables, JsonSerializerOptions options) : IViewRows
{
    /// <summary>Empty tables for every view.</summary>
    /// <returns>The tables.</returns>
    public static Dictionary<string, SortedDictionary<string, string>> Empty() =>
        ViewNames.All.ToDictionary(v => v, _ => new SortedDictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>A deep copy of <paramref name="source"/>.</summary>
    /// <param name="source">The tables.</param>
    /// <returns>The copy.</returns>
    public static Dictionary<string, SortedDictionary<string, string>> Copy(Dictionary<string, SortedDictionary<string, string>> source) =>
        source.ToDictionary(t => t.Key, t => new SortedDictionary<string, string>(t.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return ValueTask.FromResult<Result<TRow?>>(Unknown(view));
        }

        if (!table.TryGetValue(key, out var text))
        {
            return ValueTask.FromResult<Result<TRow?>>((TRow?)null);
        }

        var row = AwJson.Deserialize<TRow>(Encoding.UTF8.GetBytes(text), options);
        return ValueTask.FromResult(row.IsError ? Results.Failure<TRow?>(row.Error) : Results.Success<TRow?>(row.Value));
    }

    /// <summary>Every row of <paramref name="view"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">The view.</param>
    /// <returns>The rows.</returns>
    public Result<IReadOnlyList<TRow>> List<TRow>(string view)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return Unknown(view);
        }

        var rows = new List<TRow>();
        foreach (var text in table.Values)
        {
            var row = AwJson.Deserialize<TRow>(Encoding.UTF8.GetBytes(text), options);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    public ValueTask<Result<Unit>> Put<TRow>(string view, string key, TRow row, CancellationToken cancellationToken)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return ValueTask.FromResult<Result<Unit>>(Unknown(view));
        }

        table[key] = Encoding.UTF8.GetString(AwJson.Serialize(row, options));
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<Result<Unit>> Delete(string view, string key, CancellationToken cancellationToken)
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return ValueTask.FromResult<Result<Unit>>(Unknown(view));
        }

        _ = table.Remove(key);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    private static Error Unknown(string view) => SindriErrors.InvalidInput.With($"'{view}' isn't a view");
}
