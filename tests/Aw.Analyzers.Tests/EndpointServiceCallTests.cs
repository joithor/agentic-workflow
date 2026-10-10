using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class EndpointServiceCallTests
{
    // Stand-ins with the real namespaces and names: the rule matches the method's containing type by full name.
    private const string Stubs = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        namespace Microsoft.AspNetCore.Routing { public interface IEndpointRouteBuilder { } }
        namespace Microsoft.AspNetCore.Builder
        {
            public static class EndpointRouteBuilderExtensions
            {
                public static object MapGet(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder e, string p, Delegate d) => d;
                public static object MapPost(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder e, string p, Delegate d) => d;
            }
        }
        namespace Aw.Kernel.Services
        {
            public interface IService<in TRequest, TResponse> { ValueTask<TResponse> Handle(TRequest r, CancellationToken ct); }
        }
        namespace Aw.Probe.Host
        {
            public interface IStart : Aw.Kernel.Services.IService<int, int> { }
            public sealed class NotAService { public ValueTask<int> Handle(int r, CancellationToken ct) => new(r); }
        }
        """;

    private const string Endpoints = """
        using Aw.Kernel.Services;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Routing;
        namespace Aw.Probe.Host;
        internal static class Endpoints
        {
            internal static void Map(IEndpointRouteBuilder app, Other other)
            {
                app.MapGet("/one", (int id, IService<int, int> svc, System.Threading.CancellationToken ct) => svc.Handle(id, ct));
                app.MapGet("/none", () => 42);
                app.MapPost("/two", async (int id, IService<int, int> a, IService<int, string> b, System.Threading.CancellationToken ct) =>
                    await b.Handle(await a.Handle(id, ct), ct));
                other.MapGet("/not-an-endpoint", () => 1);
                app.MapGet("/derived", (int id, IStart svc, System.Threading.CancellationToken ct) => svc.Handle(id, ct));
                app.MapGet("/other-handle", (int id, NotAService svc, System.Threading.CancellationToken ct) => svc.Handle(id, ct));
            }
        }
        internal sealed class Other { internal void MapGet(string p, System.Func<int> f) { } }
        """;

    [Fact]
    public async Task Flags_endpoint_lambdas_that_call_zero_or_several_services()
    {
        var found = await AnalyzerHarness.Run(
            new EndpointServiceCallAnalyzer(), "Aw.Probe.Host", ("Stubs.cs", Stubs), ("Endpoints.cs", Endpoints));

        // Line 14: through an interface derived from IService<,> counts as one call. Line 15: a Handle method on
        // some other type is not a service call.
        Assert.Equal(["AW1004@10", "AW1004@11", "AW1004@15"], found);
    }

    [Fact]
    public async Task Message_states_the_measured_count()
    {
        var diagnostics = await AnalyzerHarness.Diagnostics(
            new EndpointServiceCallAnalyzer(), "Aw.Probe.Host", ("Stubs.cs", Stubs), ("Endpoints.cs", Endpoints));

        var messages = diagnostics.Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Contains(messages, m => m.Contains("makes 0 IService<,>.Handle calls", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("makes 2 IService<,>.Handle calls", StringComparison.Ordinal));
    }
}
