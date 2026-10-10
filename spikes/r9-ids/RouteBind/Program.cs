// R9 probe: does a Vogen (Conversions.None) id bind from a route value under the RDG + NativeAOT?
using System.Net;
using Spike.R9;

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
var port = args.Length > 0 ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 5210;
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
var app = builder.Build();
// Boundary adapter: the RDG passes an uninitialized id for an empty query value (`?id=`) instead of calling
// TryParse, and Vogen throws on first use. Map that to 400 here (Host is where catch is allowed, §4b.3).
app.Use(async (ctx, next) =>
{
    try { await next(ctx); }
    catch (Vogen.ValueObjectValidationException) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});
app.MapGet("/items/{id}", (ItemId id) => $"item {id.Value} initialized={id.IsInitialized()}");
app.MapGet("/budget/{b}", (TokenBudget b) => $"budget {b.Value}");
app.MapGet("/query", (ItemId id) => $"query {id.Value}");
app.MapGet("/qbudget", (TokenBudget b) => $"qbudget {b.Value}");
app.Run();
