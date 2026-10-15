namespace Aw.Sindri.Infrastructure.Views;

/// <summary>One view's table and its statements; <paramref name="Name"/> comes only from <see cref="ViewSql"/>'s fixed map.</summary>
/// <param name="Name">The table name.</param>
internal sealed record ViewTable(string Name)
{
    public string Select => $"SELECT row_json FROM {Name} WHERE key = $key";

    public string SelectAll => $"SELECT row_json FROM {Name} ORDER BY key";

    /// <summary>Keys after <c>$after</c> (exclusive), in order, at most <c>$max</c>.</summary>
    public string SelectAfter => $"SELECT row_json FROM {Name} WHERE key > $after ORDER BY key LIMIT $max";

    /// <summary>Keys between <c>$after</c> and <c>$before</c> (both exclusive), in order, at most <c>$max</c>.</summary>
    public string SelectBetween => $"SELECT row_json FROM {Name} WHERE key > $after AND key < $before ORDER BY key LIMIT $max";

    public string Upsert =>
        $"INSERT INTO {Name} (key, row_json, seq) VALUES ($key, $row, $seq) ON CONFLICT (key) DO UPDATE SET row_json = excluded.row_json, seq = excluded.seq";

    public string Delete => $"DELETE FROM {Name} WHERE key = $key";

    public string Clear => $"DELETE FROM {Name}";
}
