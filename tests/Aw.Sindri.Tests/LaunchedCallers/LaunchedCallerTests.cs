using System.Net;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.LaunchedCallers;

/// <summary>
/// A launched agent (#1002): a request that carries a launch's secret is that launch's session, whatever else it can read (the human
/// token file sits in its environment), and a session is refused the human-only verbs with SND-AUTH-002.
/// </summary>
public sealed class LaunchedCallerTests(LaunchedCallerTests.Host host) : IClassFixture<LaunchedCallerTests.Host>
{
    private const string LaunchHeader = "X-Sindri-Launch";
    private const string Secret = "proof";
    private const string Launch = "LCH-00000000000000000000000000000a01";
    private const string Instance = "ITM-00000000000000000000000000000c01/line/1/scoping.1";
    private const string Decision = "DEC-00000000000000000000000000000b01";
    private const string RecipeHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    [Fact, Trait("Lane", "integration")]
    public async Task Launch_secret_with_this_starts_human_token_is_a_session()
    {
        var answer = await host.Http.Ask(Get("/test/launched-caller", [.. Human(host.Token.Value), (SindriHeaders.LaunchSecret, "proof")]));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Session", answer.Body);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("takeover")]
    [InlineData("cancel")]
    [InlineData("start")]
    [InlineData("decide")]
    [InlineData("approve")]
    public async Task A_launched_session_is_refused_a_human_only_verb_with_SND_AUTH_002(string verb)
    {
        LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), Secret);
        (string Name, string Value)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (LaunchHeader, Launch)];

        var answer = await host.Http.Ask(Request(verb, session));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (answer.Status, answer.Code));
    }

    private static HttpRequestMessage Request(string verb, (string Name, string Value)[] headers) => verb switch
    {
        "takeover" => Post($"/sindri/workflow/instances/{Uri.EscapeDataString(Instance)}/commands", """{"kind":"takeover"}""", headers),
        "cancel" => Post($"/sindri/workflow/instances/{Uri.EscapeDataString(Instance)}/commands", """{"kind":"cancel"}""", headers),
        "start" => Post("/sindri/workflow/instances", """{"recipe":"workflow.step","item":"ITM-00000000000000000000000000000c01","nextAttempt":false,"facts":{}}""", headers),
        "decide" => Post($"/sindri/decisions/{Decision}", """{"choice":"accept"}""", headers),
        _ => Post($"/sindri/workflow/recipes/workflow.step@{RecipeHash}/approve", null, headers),
    };

    /// <summary>One control plane for the class (CallerHost.Start's steps) with a test-only route that answers the caller's class.</summary>
    public sealed class Host : IAsyncLifetime
    {
        private readonly TempHome _home = new();
        private WebApplication? _app;

        public HumanToken Token { get; } = new("token-of-this-start");

        public WebApplication App => _app ?? throw new InvalidOperationException("the host isn't started");

        public HttpClient Http { get; private set; } = new();

        public async ValueTask InitializeAsync()
        {
            var home = _home.Path;
            var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => home, _ => null }, TimeProvider.System, NoTemporal.Use);
            _ = Pipeline.Configure(app);
            _ = app.MapGet("/test/launched-caller", static (ICallerContextAccessor callers) =>
                Results.Text(callers.Current?.Class.ToString() ?? "none"));
            await using (var scope = app.Services.CreateAsyncScope())
            {
                _ = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>()
                    .Handle(new StartProcessRequest(), CancellationToken.None)).Value;
            }

            app.Services.GetRequiredService<HeldHumanToken>().Hold(Token);
            await app.StartAsync(CancellationToken.None);
            _app = app;
            Http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            if (_app is not null)
            {
                await _app.StopAsync(CancellationToken.None);
                await _app.DisposeAsync();
            }

            _home.Dispose();
        }
    }
}
