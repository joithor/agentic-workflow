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
// `default(ItemId)` is rejected at compile time (VOG009): scripts/check-default-probe.sh.
var size = TokenBudget.From(1200);
try { TokenBudget.From(-1); failures.Add("negative budget rejected"); } catch (ValueObjectValidationException) { }

var payload = new Payload(id, size, "alpha");
var json = JsonSerializer.Serialize(payload, SpikeJson.Default.Payload);
Console.WriteLine($"hybrid: json {json}");
Check(json == "{\"id\":\"item-1\",\"budget\":1200,\"name\":\"alpha\"}", "json shape");
Check(JsonSerializer.Deserialize(json, SpikeJson.Default.Payload) == payload, "json round trip");

// Boundary cases: each must be a JsonException (400), never a default id or a Vogen exception (500).
foreach (var (label, bad) in new[]
{
    ("missing id", """{"budget":1,"name":"x"}"""),
    ("null id", """{"id":null,"budget":1,"name":"x"}"""),
    ("empty id", """{"id":"","budget":1,"name":"x"}"""),
    ("numeric id", """{"id":7,"budget":1,"name":"x"}"""),
    ("negative budget", """{"id":"a","budget":-1,"name":"x"}"""),
    ("missing name", """{"id":"a","budget":1}"""),
})
{
    string outcome;
    try { var p = JsonSerializer.Deserialize(bad, SpikeJson.Default.Payload); outcome = $"accepted {p}"; }
    catch (JsonException) { outcome = "JsonException"; }
    catch (Exception e) { outcome = e.GetType().Name; }
    Console.WriteLine($"hybrid: {label}: {outcome}");
    Check(outcome == "JsonException", $"{label} -> JsonException");
}

// Without RespectRequiredConstructorParameters a missing id is silently default(ItemId): the converter never runs.
{
    var lax = JsonSerializer.Deserialize("""{"budget":1,"name":"x"}""", LaxJson.Default.Payload)!;
    var initialized = lax.Id.IsInitialized();
    Console.WriteLine($"hybrid: missing id under STJ defaults: accepted, Id.IsInitialized()={initialized}");
    Check(!initialized, "STJ defaults let an uninitialized id through (the gap Respect* closes)");
}

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

