using Aw.Kernel;
using Aw.Kernel.AotSmoke;
using Aw.Kernel.Json;

var options = NodeJson.CreateOptions(SmokeContext.Default);
var sample = new Sample(
    "smoke",
    new DateTimeOffset(2026, 10, 10, 8, 0, 0, 123, TimeSpan.FromHours(2)),
    ["</script>&'", "tab\tquote\"", "lone \ud800", "emoji \U0001F600", "line\u2028end", "plus + backtick ` caf\u00e9 \u4e2d"],
    [0.1 + 0.2, 1e21, 1e-7, -0.0, 123.456],
    KernelErrors.NotFound.With("gone"),
    new Dictionary<string, int>(StringComparer.Ordinal) { ["caf\u00e9 + ` \U0001F600"] = 1 });
var stdout = Console.OpenStandardOutput();
stdout.Write(NodeJson.Serialize(sample, options));
stdout.Write("\n"u8);
stdout.Write(NodeJson.Serialize(sample, options, indented: true));
var back = NodeJson.Deserialize<Sample>(NodeJson.Serialize(sample, options), options);
stdout.Write(System.Text.Encoding.UTF8.GetBytes($"\nroundtrip:{back.IsSuccess && back.Value.Texts[2] == sample.Texts[2]}\n"));
var bad = NodeJson.Deserialize<Sample>("{\"id\":"u8, options);
stdout.Write(System.Text.Encoding.UTF8.GetBytes($"bad:{bad.Error.Code}\n"));
var scalars = new Scalars(new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc), Money: 1.10m, Big: 9007199254740993L, Single: 0.1f, Nan: double.NaN);
stdout.Write(NodeJson.Serialize(scalars, options));
stdout.Write("\n"u8);
stdout.Write(NodeJson.Restringify("{\"b\":1.0,\"1\":2,\"s\":\"caf\\u00e9 \\ud800\",\"e\":1E5,\"dup\":1,\"dup\":2}"u8).Value);
stdout.Write("\n"u8);
