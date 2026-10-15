using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Ids;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Schemas;

/// <summary>
/// The JSON Schemas for api-run scripts and summaries are generated from the records and committed under schemas/evidence/. A
/// record change that isn't regenerated fails here; regenerate with AW_GOLDEN_UPDATE=1 (refused in CI) and review the diff.
/// </summary>
public sealed class EvidenceSchemaTests
{
    public static TheoryData<string> Files => ["api-script", "api-summary"];

    internal static JsonObject Committed(string name) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(PathOf(name))));

    [Theory, Trait("Lane", "integration")]   // reads (and in update mode writes) schemas/evidence/
    [MemberData(nameof(Files))]
    public void The_committed_schema_is_generated_from_the_record(string name)
    {
        var actual = Render(Generate(name == "api-script" ? typeof(ApiScript) : typeof(ApiSummary), name));
        var path = PathOf(name);

        if (string.Equals(Environment.GetEnvironmentVariable(Golden.UpdateVariable), "1", StringComparison.Ordinal)
            && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.Ordinal))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            Assert.Fail($"{path} regenerated; review the diff, then run again without {Golden.UpdateVariable}.");
        }

        Assert.True(File.Exists(path), $"{path} is missing; generate it with {Golden.UpdateVariable}=1.");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static string PathOf(string name) =>
        Path.GetFullPath(Path.Combine(Golden.SourcePath(typeof(EvidenceSchemaTests).Assembly, "Schemas"), "..", "..", "..", "schemas", "evidence", $"{name}.schema.json"));

    private static JsonObject Generate(Type type, string name)
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(TestJson.Options, type, new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) => Transform(context.TypeInfo.Type, node),
        });
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://github.com/jthorare/agentic-workflow/schemas/evidence/{name}.schema.json",
            ["title"] = name == "api-script" ? "ui-evidence api-run script (evidence.api.run/1)" : "ui-evidence api-run summary (evidence.api.summary/1)",
        };
        foreach (var (key, value) in schema.AsObject().ToList())
        {
            root[key] = value?.DeepClone();
        }

        return root;
    }

    // A typed id and a header source have user-code converters, so the exporter can't see their shape ('true'): describe them.
    private static JsonNode Transform(Type type, JsonNode node)
    {
        if (type == typeof(RunId) || type == typeof(RunInstanceId))
        {
            return new JsonObject { ["type"] = "string", ["pattern"] = "\\S" };
        }

        if (type == typeof(HeaderSource))
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["env"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[A-Z][A-Z0-9_]*$" } },
                ["required"] = new JsonArray("env"),
                ["additionalProperties"] = false,
            };
        }

        return node;
    }

    private static string Render(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }
}
