using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// The control plane's three instance read routes as the CLI's HTTP client sees them (engine spec §6.3, review run 2,
/// architecture-strategist-r2-4): each request calls the real slice service and answers as the host's results do (200
/// with the response, else a problem whose title is the error code). The CLI's reads are clients of these routes and
/// never start the control plane, so the desk's start rule throws.
/// </summary>
internal sealed class ReadApi : HttpMessageHandler
{
    private const string Instances = "/sindri/workflow/instances";
    private readonly ServiceProvider _services;
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private readonly ControlPlaneClient? _client;

    private ReadApi(Action<IServiceCollection> configure, bool running)
    {
        var services = new ServiceCollection();
        configure(services);
        _services = services.BuildServiceProvider();
        var probe = running ? FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3)) : new FakeControlPlaneProbe();
        _client = new ControlPlaneClient(probe, SliceHarness.Json, handler: this);
        Desk = new RecipeDesk(_client, TextReader.Null, static _ => throw new InvalidOperationException("a read never starts the control plane"));
    }

    /// <summary>What the CLI is given: this API's client and a start rule that must not be called.</summary>
    public RecipeDesk Desk { get; }

    /// <summary>A snapshot of every request's path and query, in order (taken under a lock: the CLI under test calls from another thread).</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The routes over the instance <paramref name="instances"/> holds and the ledger services <paramref name="configure"/> adds.</summary>
    /// <param name="instances">The one instance the directory and reader know.</param>
    /// <param name="configure">Adds the services the routes call.</param>
    /// <returns>The API.</returns>
    public static ReadApi Over(OneInstance instances, Action<IServiceCollection>? configure = null) => new(services =>
    {
        _ = services
            .AddSingleton<IInstanceDirectory>(instances)
            .AddSingleton<IInstanceReader>(instances)
            .AddSingleton<IService<GetInstanceRequest, GetInstanceResponse>>(new GetInstanceService(instances, instances))
            .AddSingleton<IService<ListInstancesRequest, ListInstancesResponse>>(new ListInstancesService(instances, new NoRows(), instances));
        configure?.Invoke(services);
    }, running: true);

    /// <summary>An API that is not running: the probe finds no control plane.</summary>
    /// <returns>The API.</returns>
    public static ReadApi Down() => new(static _ => { }, running: false);

    /// <summary>The service the caller registered, for tests that need to read what the routes read.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service.</returns>
    public T Get<T>()
        where T : notnull => _services.GetRequiredService<T>();

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;   // why: HttpClient always sets the URI
        lock (_gate)
        {
            _calls.Add(uri.PathAndQuery);
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        var json = SliceHarness.Json;
        if (uri.AbsolutePath == Instances)
        {
            var item = query["item"];
            var list = new ListInstancesRequest(query["needsMe"], item is null ? null : ItemKey.From(item), query["state"], query["query"], query["address"]);
            return Answer(await _services.GetRequiredService<IService<ListInstancesRequest, ListInstancesResponse>>().Handle(list, cancellationToken).ConfigureAwait(false), json);
        }

        var rest = uri.AbsolutePath[(Instances.Length + 1)..].Split('/');
        return rest switch
        {
            [var id] => Answer(await _services.GetRequiredService<IService<GetInstanceRequest, GetInstanceResponse>>().Handle(new GetInstanceRequest(id), cancellationToken).ConfigureAwait(false), json),
            [var id, "events"] => Answer(await _services.GetRequiredService<IService<GetInstanceEventsRequest, GetInstanceEventsResponse>>()
                .Handle(new GetInstanceEventsRequest(id, long.Parse(query["after"] ?? "0", System.Globalization.CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false), json),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();
            _services.Dispose();
        }

        base.Dispose(disposing);
    }

    private static HttpResponseMessage Answer<T>(Result<T> result, JsonSerializerOptions json) => result.IsSuccess
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(AwJson.Serialize(result.Value, json)) { Headers = { ContentType = new("application/json") } } }
        : Problem(result.Error);

    private static HttpResponseMessage Problem(Error error)
    {
        var status = error.Kind == ErrorKind.NotFound ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest;
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { type = "about:blank", title = error.Code, status = (int)status, detail = error.Message }), Encoding.UTF8, "application/problem+json"),
        };
    }

    private sealed class NoRows : IWorkflowInstances
    {
        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
            List(InstanceFilters.ToQuery(filter), ended, cancellationToken);

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(Array.Empty<InstanceRow>());
    }
}
