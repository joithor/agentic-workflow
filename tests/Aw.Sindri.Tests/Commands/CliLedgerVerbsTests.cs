using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri ledger' (API P5 unit F): a client of GET /sindri/ledger.</summary>
public sealed class CliLedgerVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_ledger_prints_lines_and_next()
    {
        var page = new LedgerPage([Event(1, "process.started", item: null), Event(2, "item.created", A.FirstItemId)], Next: 2);
        var api = new Api(HttpStatusCode.OK, Body(page));

        var code = await Verbs(api).Run(["--after", "0", "--max", "2", "--type", "item.*,process.*", "--item", "ENG-142"], TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal($"1  2026-01-01T00:00:00Z  process.started  service:host\n2  2026-01-01T00:00:00Z  item.created  service:host  {A.FirstItemId.Value}\nnext: 2\n", _out.ToString().ReplaceLineEndings("\n"));
        Assert.Equal("?after=0&max=2&type=item.%2A%2Cprocess.%2A&item=ENG-142", api.Query);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_ledger_json_is_the_raw_body()
    {
        var body = Body(new LedgerPage([Event(1, "process.started", item: null)], Next: null));

        var code = await Verbs(new Api(HttpStatusCode.OK, body)).Run(["--json"], TestContext.Current.CancellationToken);

        Assert.Equal((0, body + "\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--after", "x")]
    [InlineData("--wat", "1")]
    [InlineData("--max", "")]
    public async Task Cli_ledger_refuses_a_bad_argument_without_calling(string flag, string value)
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(api).Run(value.Length == 0 ? [flag] : [flag, value], TestContext.Current.CancellationToken);

        Assert.Equal((2, 0), (code, api.Calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_ledger_reports_the_apis_refusal()
    {
        var api = new Api(HttpStatusCode.BadRequest, """{"title":"SND-CLI-003","status":400,"detail":"after 9 is past the ledger's head 3"}""");

        var code = await Verbs(api).Run(["--after", "9"], TestContext.Current.CancellationToken);

        Assert.NotEqual(0, code);
        Assert.Contains("SND-CLI-003", _error.ToString(), StringComparison.Ordinal);
    }

    private static string Body(LedgerPage page) => Encoding.UTF8.GetString(AwJson.Serialize(page, SliceHarness.Json));

    private static PublicEvent Event(long sequence, string type, ItemId? item) =>
        new(sequence, EventId.From(new Guid((int)sequence, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), type, 1, KnownInstants.Epoch, "service:host", "local", 0, item,
            CorrelationId.From(new Guid(9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), CausationId: null, TraceId: null, JsonDocument.Parse("{}").RootElement);

    private LedgerVerbs Verbs(Api api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new LedgerVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Query { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Query = request.RequestUri?.Query;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") });
        }
    }
}
