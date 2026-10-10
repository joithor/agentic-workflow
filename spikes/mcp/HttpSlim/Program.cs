// MCP spike (c): Streamable HTTP MCP server on ASP.NET Core CreateSlimBuilder, loopback-only in code.
//   HttpSlim <port>              hardened: no args or environment reach the web host's configuration, Kestrel
//                                listens on 127.0.0.1:<port> only, Host must be loopback (B12, DNS rebinding),
//                                and a browser Origin must be loopback (MCP Streamable HTTP requirement).
//   HttpSlim <port> --baseline   the first spike's setup (CreateSlimBuilder(args), configuration intact,
//                                no Host/Origin checks), kept so scripts/drive-http.sh can show the difference.
using System.ComponentModel;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HostFiltering;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var port = int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
var baseline = args.Contains("--baseline");

var builder = WebApplication.CreateSlimBuilder(baseline ? new WebApplicationOptions { Args = args } : new WebApplicationOptions { Args = [] });
if (!baseline)
{
    // Every override vector (Kestrel:Endpoints, urls, http_ports, PreferHostingUrls) comes in through
    // configuration: command-line args and environment variables. The bridge reads its own settings file,
    // so the web host gets none of them.
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddInMemoryCollection();   // a writable source for the pinned settings below
    // ASPNETCORE_-prefixed variables are host settings, read when the builder is created, so clearing the
    // sources is too late for them: pin the two that decide binding.
    builder.WebHost.PreferHostingUrls(false);
    builder.WebHost.UseUrls();
}
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
if (!baseline)
{
    builder.Services.Configure<HostFilteringOptions>(o =>
    {
        o.AllowedHosts = ["127.0.0.1", "localhost", "[::1]"];
        o.AllowEmptyHosts = false;
    });
}

var json = new System.Text.Json.JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
json.TypeInfoResolverChain.Insert(0, ToolJson.Default);

builder.Services
    .AddMcpServer(o => o.ServerInfo = new Implementation { Name = "spike-http-slim", Version = "0.0.1" })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<UpperTools>(json);

var app = builder.Build();
if (!baseline)
{
    app.UseHostFiltering();
    app.Use(async (ctx, next) =>
    {
        // Non-browser clients send no Origin; a browser page may only come from a loopback origin.
        var origin = ctx.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !LoopbackOrigin.IsAllowed(origin))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(ctx);
    });
}
app.MapMcp("/mcp");
app.Run();

static class LoopbackOrigin
{
    public static bool IsAllowed(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
        && (u.Host == "127.0.0.1" || u.Host == "localhost" || u.Host == "[::1]");
}

[McpServerToolType]
public sealed class UpperTools
{
    [McpServerTool(Name = "upper"), Description("Uppercases text.")]
    public static string Upper([Description("Text to uppercase.")] string text) => text.ToUpperInvariant();
}

[JsonSerializable(typeof(string))]
internal sealed partial class ToolJson : JsonSerializerContext;
