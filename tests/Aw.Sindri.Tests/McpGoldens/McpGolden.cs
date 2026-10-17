using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.McpGoldens;

/// <summary>Records a whole tool result (isError, structuredContent, the text blocks) under Golden/Mcp, normalized as the API goldens are (D6).</summary>
internal static class McpGolden
{
    /// <summary>Compares <paramref name="result"/> with Golden/Mcp/&lt;tool&gt;/&lt;scenario&gt;.json.</summary>
    /// <param name="result">The tool result.</param>
    /// <param name="home">The host's temp home, normalized to '&lt;home&gt;'.</param>
    /// <param name="tool">The tool's name.</param>
    /// <param name="scenario">The scenario's name.</param>
    public static void Match(CallToolResult result, string home, string tool, string scenario)
    {
        ArgumentNullException.ThrowIfNull(result);
        var json = new JsonObject
        {
            ["isError"] = result.IsError == true,
            ["structuredContent"] = result.StructuredContent is { } structured ? JsonNode.Parse(structured.GetRawText()) : null,
            ["content"] = new JsonArray([.. result.Content.OfType<TextContentBlock>().Select(b => (JsonNode?)JsonValue.Create(b.Text))]),
        };
        Golden.Match(
            ApiGolden.Reindent(json.ToJsonString()),
            typeof(McpGolden).Assembly,
            $"Golden/Mcp/{tool}/{scenario}.json",
            new Normalization(
                new NormalizationRule("home", System.Text.RegularExpressions.Regex.Escape(home), "<home>"),
                new NormalizationRule("port", "port [0-9]+", "port <port>"),
                new NormalizationRule("build", "sindri [^,\"]+, port", "sindri <build>, port")));
    }
}
