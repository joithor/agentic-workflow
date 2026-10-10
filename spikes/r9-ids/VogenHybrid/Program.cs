// R9 variant (c): Vogen for validation and the default() ban, with Conversions.None and hand-written
// System.Text.Json converters declared in user code, where the STJ source generator can see them.
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Spike.R9;
using Vogen;

var failures = new List<string>();
void Check(bool ok, string what) { if (!ok) failures.Add(what); }

var id = ItemId.From("item-1");
Check(id.Value == "item-1", "From/Value");
Check(ItemId.From("item-1") == id, "value equality");
try { ItemId.From(""); failures.Add("empty id rejected"); } catch (ValueObjectValidationException) { }
// `default(ItemId)` is rejected at compile time by Vogen's analyzer (VOG009).
var size = TokenBudget.From(1200);
try { TokenBudget.From(-1); failures.Add("negative budget rejected"); } catch (ValueObjectValidationException) { }

var payload = new Payload(id, size, "alpha");
var json = JsonSerializer.Serialize(payload, SpikeJson.Default.Payload);
Console.WriteLine($"hybrid: json {json}");
Check(json == "{\"id\":\"item-1\",\"budget\":1200,\"name\":\"alpha\"}", "json shape");
Check(JsonSerializer.Deserialize(json, SpikeJson.Default.Payload) == payload, "json round trip");

var db = args.Length > 0 ? args[0] : "hybrid.db";
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
        Console.WriteLine($"hybrid: db {r.GetString(0)} {r.GetInt32(1)}");
    }
}

foreach (var f in failures) Console.Error.WriteLine($"hybrid: FAIL {f}");
Console.WriteLine(failures.Count == 0 ? "hybrid: OK" : $"hybrid: FAILED {failures.Count}");
return failures.Count == 0 ? 0 : 1;

namespace Spike.R9
{
    [ValueObject<string>(conversions: Conversions.None)]
    [JsonConverter(typeof(ItemIdJsonConverter))]
    public readonly partial struct ItemId
    {
        private static Validation Validate(string value) =>
            string.IsNullOrWhiteSpace(value) ? Validation.Invalid("ItemId must not be empty") : Validation.Ok;
    }

    public sealed class ItemIdJsonConverter : JsonConverter<ItemId>
    {
        public override ItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ItemId.From(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, ItemId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }

    [ValueObject<int>(conversions: Conversions.None)]
    [JsonConverter(typeof(TokenBudgetJsonConverter))]
    public readonly partial struct TokenBudget
    {
        private static Validation Validate(int value) =>
            value < 0 ? Validation.Invalid("TokenBudget must be >= 0") : Validation.Ok;
    }

    public sealed class TokenBudgetJsonConverter : JsonConverter<TokenBudget>
    {
        public override TokenBudget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => TokenBudget.From(reader.GetInt32());
        public override void Write(Utf8JsonWriter writer, TokenBudget value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
    }

    public sealed record Payload(ItemId Id, TokenBudget Budget, string Name);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Payload))]
    public sealed partial class SpikeJson : JsonSerializerContext;
}
