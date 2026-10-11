using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host;

/// <summary>The request pipeline Program.cs runs: the loopback guard, Vogen's 400 mapping, then the endpoint table.</summary>
internal static class Pipeline
{
    internal static WebApplication Configure(WebApplication app)
        => app.UseLoopbackGuard().UseValueObjectValidation().MapSindriEndpoints();
}
