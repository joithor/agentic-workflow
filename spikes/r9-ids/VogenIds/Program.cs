// R9 variant (a): Vogen-generated strongly typed ids under NativeAOT.
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Spike.R9;
using Vogen;

var failures = new List<string>();
void Check(bool ok, string what) { if (!ok) failures.Add(what); }

// 1. Construction and validation
var id = ItemId.From("item-1");
Check(id.Value == "item-1", "From/Value");
Check(ItemId.From("item-1") == id, "value equality");
try { ItemId.From(""); failures.Add("empty id rejected"); } catch (ValueObjectValidationException) { }
// `default(ItemId)` is rejected at compile time by Vogen's analyzer (VOG009); see docs/readiness/aot-spike.md.
var size = TokenBudget.From(1200);
try { TokenBudget.From(-1); failures.Add("negative budget rejected"); } catch (ValueObjectValidationException) { }

// 2. System.Text.Json source generation round trip
var payload = new Payload(id, size, "alpha");
var json = JsonSerializer.Serialize(payload, SpikeJson.Default.Payload);
Console.WriteLine($"vogen: json {json}");
Check(json == "{\"id\":\"item-1\",\"budget\":1200,\"name\":\"alpha\"}", "json shape");
var back = JsonSerializer.Deserialize(json, SpikeJson.Default.Payload);
Check(back == payload, "json round trip");

// 3. ADO.NET binding + reading back into the id type
var db = args.Length > 0 ? args[0] : "vogen.db";
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
        var readId = ItemId.From(r.GetString(0));
        var readBudget = TokenBudget.From(r.GetInt32(1));
        Check(readId == id && readBudget == size, "db round trip");
        Console.WriteLine($"vogen: db {readId} {readBudget}");
    }
}

foreach (var f in failures) Console.Error.WriteLine($"vogen: FAIL {f}");
Console.WriteLine(failures.Count == 0 ? "vogen: OK" : $"vogen: FAILED {failures.Count}");
return failures.Count == 0 ? 0 : 1;

namespace Spike.R9
{
    [ValueObject<string>(conversions: Conversions.SystemTextJson)]
    public readonly partial struct ItemId
    {
        private static Validation Validate(string value) =>
            string.IsNullOrWhiteSpace(value) ? Validation.Invalid("ItemId must not be empty") : Validation.Ok;
    }

    [ValueObject<int>(conversions: Conversions.SystemTextJson)]
    public readonly partial struct TokenBudget
    {
        private static Validation Validate(int value) =>
            value < 0 ? Validation.Invalid("TokenBudget must be >= 0") : Validation.Ok;
    }

    public sealed record Payload(ItemId Id, TokenBudget Budget, string Name);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Payload))]
    public sealed partial class SpikeJson : JsonSerializerContext;
}
