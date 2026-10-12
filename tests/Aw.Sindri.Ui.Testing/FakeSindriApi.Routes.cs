using System.Net;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The routes U1 adds (<see cref="FakeSindriApi"/>, continued): each answers its <see cref="RouteAnswer"/>.</summary>
internal sealed partial class FakeSindriApi
{
    private int _catalogCalls;
    private int _servicesCalls;
    private int _rebuildCalls;

    /// <summary>When set, a human caller must send this as X-Sindri-Token, as API P1 requires (the handler answers SND-AUTH-001 otherwise).</summary>
    public string? HumanToken { get; set; }

    /// <summary>GET /sindri/commands's answer: P1's golden catalog by default.</summary>
    public RouteAnswer Catalog { get; set; } = RouteAnswer.Api("commands", "catalog");

    /// <summary>GET /sindri/services's answer: a spec-shaped healthy view by default (P2 isn't built).</summary>
    public RouteAnswer ServiceView { get; set; } = RouteAnswer.SpecShaped("services", "fixture");

    /// <summary>POST /sindri/views/rebuild's answer: a spec-shaped accepted command by default.</summary>
    public RouteAnswer Rebuild { get; set; } = RouteAnswer.SpecShaped("views-rebuild", "accepted");

    /// <summary>When set, each rebuild waits for it before it answers (a command in flight); null answers at once.</summary>
    public TaskCompletionSource? RebuildGate { get; set; }

    /// <summary>How many catalog reads were made.</summary>
    public int CatalogCalls => Volatile.Read(ref _catalogCalls);

    /// <summary>How many services reads were made.</summary>
    public int ServicesCalls => Volatile.Read(ref _servicesCalls);

    /// <summary>How many rebuild commands were sent.</summary>
    public int RebuildCalls => Volatile.Read(ref _rebuildCalls);

    public Task<Result<CommandList>> Commands(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _catalogCalls);
        return Task.FromResult(Answer<CommandList>(api, Catalog));
    }

    public Task<Result<ServiceList>> Services(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _servicesCalls);
        return Task.FromResult(Answer<ServiceList>(api, ServiceView));
    }

    public async Task<Result<CommandAcceptedView>> RebuildViews(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _rebuildCalls);
        if (RebuildGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<CommandAcceptedView>(api, Rebuild);
    }

    // What the real client makes of the same answer: the record, or the refusal's code and detail as served.
    private Result<T> Answer<T>(Uri api, RouteAnswer answer)
    {
        if (Down)
        {
            return SindriUiErrors.Unreachable.With($"nothing answers at {api.Authority}");
        }

        if (answer.IsSuccess)
        {
            return AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(answer.Body), SindriApiClient.Json);
        }

        var problem = AwJson.Deserialize<ProblemView>(System.Text.Encoding.UTF8.GetBytes(answer.Body), SindriApiClient.Json).Value;
        return new Error(problem.Title, KindOf(answer.Status), problem.Detail);
    }

    private static ErrorKind KindOf(HttpStatusCode status) => (int)status switch
    {
        400 => ErrorKind.Invalid,
        403 => ErrorKind.Forbidden,
        404 => ErrorKind.NotFound,
        409 => ErrorKind.Conflict,
        _ => ErrorKind.Unavailable,
    };
}
