// MCP spike (c): Streamable HTTP MCP server on ASP.NET Core CreateSlimBuilder, bound to loopback in code.
using System.ComponentModel;
using System.Net;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateSlimBuilder(args);
var port = args.Length > 0 ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 5199;
// Loopback in code (spec §4d): ignores ASPNETCORE_URLS / Kestrel URL settings.
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));

var json = new System.Text.Json.JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
json.TypeInfoResolverChain.Insert(0, ToolJson.Default);

builder.Services
    .AddMcpServer(o => o.ServerInfo = new Implementation { Name = "spike-http-slim", Version = "0.0.1" })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<UpperTools>(json);

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

[McpServerToolType]
public sealed class UpperTools
{
    [McpServerTool(Name = "upper"), Description("Uppercases text.")]
    public static string Upper([Description("Text to uppercase.")] string text) => text.ToUpperInvariant();
}

[JsonSerializable(typeof(string))]
internal sealed partial class ToolJson : JsonSerializerContext;
