// MCP spike (b): stdio server using the SDK's attribute tools ([McpServerTool]) registered with WithTools<T>
// and a source-generated JsonSerializerContext (the SDK's documented AOT path).
using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var json = new System.Text.Json.JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
json.TypeInfoResolverChain.Insert(0, ToolJson.Default);

builder.Services
    .AddMcpServer(o => o.ServerInfo = new Implementation { Name = "spike-stdio-attr", Version = "0.0.1" })
    .WithStdioServerTransport()
    .WithTools<UpperTools>(json);

await builder.Build().RunAsync();

[McpServerToolType]
public sealed class UpperTools
{
    [McpServerTool(Name = "upper"), Description("Uppercases text.")]
    public static string Upper([Description("Text to uppercase.")] string text) => text.ToUpperInvariant();
}

[JsonSerializable(typeof(string))]
internal sealed partial class ToolJson : JsonSerializerContext;
