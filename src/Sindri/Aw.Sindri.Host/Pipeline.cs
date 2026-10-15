using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Host;

/// <summary>
/// The request pipeline Program.cs runs: the loopback guard, the Sindri-Api-Version header, the API's own problems
/// (exceptions, refused input), the caller (headers, token, prover label), read-your-writes on queries (X-Sindri-After),
/// the Origin refusal on commands (before any body is bound), Vogen's 400 mapping, then the endpoint table.
/// </summary>
internal static class Pipeline
{
    internal static WebApplication Configure(WebApplication app)
        => app.UseLoopbackGuard().UseApiVersionHeader().UseApiProblems().UseMcpCaller().UseCallerContext().UseReadYourWrites()
            .UseCommandOriginRefusal().UseValueObjectValidation().MapSindriEndpoints().MapSindriMcp();
}
