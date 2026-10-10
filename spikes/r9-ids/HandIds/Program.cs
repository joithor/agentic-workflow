// R9 variant (b): hand-written readonly record struct ids with hand-written JSON converters.
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Spike.R9;

var failures = new List<string>();
void Check(bool ok, string what) { if (!ok) failures.Add(what); }

var id = ItemId.From("item-1");
Check(id.Value == "item-1", "From/Value");
Check(ItemId.From("item-1") == id, "value equality");
try { ItemId.From(""); failures.Add("empty id rejected"); } catch (ArgumentException) { }
// A hand-written record struct cannot stop `default`: default(ItemId).Value is null. Recorded as a gap.
Check(default(ItemId).Value is null, "default instance is NOT prevented (expected gap)");
var size = TokenBudget.From(1200);
try { TokenBudget.From(-1); failures.Add("negative budget rejected"); } catch (ArgumentOutOfRangeException) { }

var payload = new Payload(id, size, "alpha");
var json = JsonSerializer.Serialize(payload, SpikeJson.Default.Payload);
Console.WriteLine($"hand: json {json}");
Check(json == "{\"id\":\"item-1\",\"budget\":1200,\"name\":\"alpha\"}", "json shape");
Check(JsonSerializer.Deserialize(json, SpikeJson.Default.Payload) == payload, "json round trip");

var db = args.Length > 0 ? args[0] : "hand.db";
using (var conn = new SqliteConnection($"Data Source={db}"))
{
    conn.Open();
    using (var c = conn.CreateCommand()) { c.CommandText = "CREATE TABLE IF NOT EXISTS ids (id TEXT PRIMARY KEY, budget INTEGER NOT NULL)"; c.ExecuteNonQuery(); }
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "INSERT OR REPLACE INTO ids (id, budget) VALUES ($id, $budget)";
        c.Parameters.AddWithValue("$id", id.Value);
        c.Parameters.AddWithValue("$budget", size.Value);
        c.ExecuteNonQuery();
    }
    using (var c = conn.CreateCommand())
    {
        c.CommandText = "SELECT id, budget FROM ids WHERE id = $id";
        c.Parameters.AddWithValue("$id", id.Value);
        using var r = c.ExecuteReader();
        Check(r.Read(), "row read");
        Check(ItemId.From(r.GetString(0)) == id && TokenBudget.From(r.GetInt32(1)) == size, "db round trip");
        Console.WriteLine($"hand: db {r.GetString(0)} {r.GetInt32(1)}");
    }
}

foreach (var f in failures) Console.Error.WriteLine($"hand: FAIL {f}");
Console.WriteLine(failures.Count == 0 ? "hand: OK" : $"hand: FAILED {failures.Count}");
return failures.Count == 0 ? 0 : 1;

namespace Spike.R9
{
    [JsonConverter(typeof(ItemIdConverter))]
    public readonly record struct ItemId(string Value)
    {
        public static ItemId From(string value) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("ItemId must not be empty", nameof(value)) : new(value);
        public override string ToString() => Value;
    }

    public sealed class ItemIdConverter : JsonConverter<ItemId>
    {
        public override ItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ItemId.From(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, ItemId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }

    [JsonConverter(typeof(TokenBudgetConverter))]
    public readonly record struct TokenBudget(int Value)
    {
        public static TokenBudget From(int value) => value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : new(value);
    }

    public sealed class TokenBudgetConverter : JsonConverter<TokenBudget>
    {
        public override TokenBudget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => TokenBudget.From(reader.GetInt32());
        public override void Write(Utf8JsonWriter writer, TokenBudget value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
    }

    public sealed record Payload(ItemId Id, TokenBudget Budget, string Name);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Payload))]
    public sealed partial class SpikeJson : JsonSerializerContext;
}
