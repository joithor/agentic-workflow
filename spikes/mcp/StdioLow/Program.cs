// MCP spike (a): stdio server using the SDK's low-level list/call handlers (no reflection-based tool binding).
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);
// stdout carries JSON-RPC; logs go to stderr only.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

using var schemaDoc = JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""");
var schema = schemaDoc.RootElement.Clone();

builder.Services
    .AddMcpServer(o => o.ServerInfo = new Implementation { Name = "spike-stdio-low", Version = "0.0.1" })
    .WithStdioServerTransport()
    .WithListToolsHandler((ctx, ct) => ValueTask.FromResult(new ListToolsResult
    {
        Tools = [new Tool { Name = "upper", Description = "Uppercases text.", InputSchema = schema }],
    }))
    .WithCallToolHandler((ctx, ct) =>
    {
        var p = ctx.Params;
        if (p?.Name != "upper")
            return ValueTask.FromResult(Error($"unknown tool: {p?.Name}"));
        if (p.Arguments is null || !p.Arguments.TryGetValue("text", out var text) || text.ValueKind != JsonValueKind.String)
            return ValueTask.FromResult(Error("argument 'text' (string) is required"));
        return ValueTask.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = text.GetString()!.ToUpperInvariant() }] });
    });

await builder.Build().RunAsync();

static CallToolResult Error(string message) => new() { IsError = true, Content = [new TextContentBlock { Text = message }] };
