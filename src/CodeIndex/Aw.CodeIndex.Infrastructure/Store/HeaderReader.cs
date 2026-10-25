using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>Reads the header (meta and layer rows) of an open index file; anything unexpected, or another schema version, is damage.</summary>
internal static class HeaderReader
{
    private const string Meta = "SELECT key, value FROM meta";
    private const string Layers = "SELECT layer, stamp, status, code, detail, built_at FROM layers";

    internal static async Task<IndexHeader> Read(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var version = await Sql.Count(connection, "PRAGMA user_version", cancellationToken).ConfigureAwait(false);
        if (version != CodeIndexRules.SchemaVersion)
        {
            throw new InvalidDataException($"the file has schema version {version.ToString(CultureInfo.InvariantCulture)}, not {CodeIndexRules.SchemaVersion.ToString(CultureInfo.InvariantCulture)}");
        }

        var meta = await ReadMeta(connection, cancellationToken).ConfigureAwait(false);
        return new IndexHeader(ToMeta(meta), await ReadLayers(connection, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<Dictionary<string, string>> ReadMeta(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        var command = Sql.Command(connection, Meta);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows[reader.GetString(0)] = reader.GetString(1);
                }
            }
        }

        return rows;
    }

    private static IndexMeta ToMeta(Dictionary<string, string> meta)
    {
        var commit = CommitSha.TryFrom(Required(meta, "commit"), out var sha) ? sha : throw new InvalidDataException("meta.commit is not a commit id");
        var builtAt = Time(Required(meta, "built_at"));
        var counts = new IndexCounts(Number(meta, "files"), Number(meta, "symbols"), Number(meta, "edges"));
        return new IndexMeta(commit, Required(meta, "default_branch"), builtAt, Number(meta, "schema"), Required(meta, "parser_id"), meta.GetValueOrDefault("lock_sha"), counts);
    }

    private static async Task<IReadOnlyList<LayerRecord>> ReadLayers(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var layers = new List<LayerRecord>();
        var command = Sql.Command(connection, Layers);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    layers.Add(new LayerRecord(
                        StoredNames.LayerOf(reader.GetString(0)),
                        reader.GetString(1),
                        StoredNames.StateOf(reader.GetString(2)),
                        Sql.Text(reader, 3),
                        reader.GetString(4),
                        Time(reader.GetString(5))));
                }
            }
        }

        return [.. layers.OrderBy(l => l.Layer)];
    }

    private static DateTimeOffset Time(string text)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : throw new InvalidDataException($"'{text}' is not a time");

    private static string Required(Dictionary<string, string> meta, string key)
        => meta.TryGetValue(key, out var value) ? value : throw new InvalidDataException($"meta.{key} is missing");

    private static int Number(Dictionary<string, string> meta, string key)
        => int.TryParse(Required(meta, key), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : throw new InvalidDataException($"meta.{key} is not a number");
}
