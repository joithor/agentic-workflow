using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri recipe register', 'recipe list [--stale] [--json]' and 'recipe show &lt;id@hash&gt;' against the real host
/// (engine spec §6.3): 'registered workflow.step@9ab2d40c1e77 (unapproved)', one line per recipe or 'no recipes
/// registered', and the preview; '--json' parses and carries each recipe's inputs (README X34).
/// </summary>
public sealed class RecipeRegistryCliTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _home.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Register_then_list_and_show_print_the_registered_recipe()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var file = await Shipped("workflow.step.yaml", ct);

        var registered = await Run(app, ct, "register", file);
        var hash = (await app.Services.GetRequiredService<IRecipeStore>().All(ct)).Value.Single().Hash;
        var name = $"workflow.step@{hash.Display}";
        var registerOut = Take();
        var again = await Run(app, ct, "register", file);
        var againOut = Take();
        var listed = await Run(app, ct, "list");
        var listOut = Take();
        var shown = await Run(app, ct, "show", name);
        var showOut = Take();

        Assert.True((registered, again, listed, shown) == (0, 0, 0, 0), _error.ToString());
        Assert.Equal($"registered {name} (unapproved)\n", registerOut);
        Assert.Equal($"already registered {name} (unapproved)\n", againOut);
        Assert.Equal($"{name} unapproved\n", listOut);
        Assert.StartsWith($"{name} unapproved\n", showOut, StringComparison.Ordinal);
        Assert.Contains("pinned blocks:\n  step.checks sha256:", showOut, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task List_json_parses_with_each_recipes_standing_and_inputs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        _ = await Run(app, ct, "register", await Shipped("workflow.step.yaml", ct));
        _ = Take();

        var code = await Run(app, ct, "list", "--json");

        Assert.True(code == 0, _error.ToString());
        var recipe = Assert.Single(JsonDocument.Parse(_out.ToString()).RootElement.GetProperty("recipes").EnumerateArray());
        Assert.Equal(("workflow.step", "unapproved"), (recipe.GetProperty("id").GetString(), recipe.GetProperty("standing").GetString()));
        Assert.Equal("fact:spec", Assert.Single(recipe.GetProperty("inputs").EnumerateArray()).GetProperty("key").GetString());
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Show_json_returns_the_recipes_fields_as_list_json_does_and_the_preview()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        _ = await Run(app, ct, "register", await Shipped("workflow.step.yaml", ct));
        var hash = (await app.Services.GetRequiredService<IRecipeStore>().All(ct)).Value.Single().Hash;
        _ = Take();
        var name = $"workflow.step@{hash.Display}";

        var after = await Run(app, ct, "show", name, "--json");
        var afterOut = Take();
        var before = await Run(app, ct, "show", "--json", name);

        Assert.True((after, before) == (0, 0), _error.ToString());
        Assert.Equal(afterOut, _out.ToString());
        var shown = JsonDocument.Parse(afterOut).RootElement;
        var recipe = shown.GetProperty("recipe");
        Assert.Equal(("workflow.step", "unapproved", "work"), (recipe.GetProperty("id").GetString(), recipe.GetProperty("standing").GetString(), recipe.GetProperty("urgency").GetString()));
        Assert.Equal(JsonValueKind.Array, recipe.GetProperty("outputs").ValueKind);
        Assert.Equal("fact:spec", Assert.Single(recipe.GetProperty("inputs").EnumerateArray()).GetProperty("key").GetString());
        Assert.True(recipe.TryGetProperty("hash", out _) && recipe.TryGetProperty("version", out _));
        Assert.Equal(JsonValueKind.Object, shown.GetProperty("preview").ValueKind);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Empty_lists_say_so()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);

        var all = await Run(app, ct, "list");
        var allOut = Take();
        var stale = await Run(app, ct, "list", "--stale");

        Assert.Equal((0, 0), (all, stale));
        Assert.Equal("no recipes registered\n", allOut);
        Assert.Equal("no stale recipes\n", _out.ToString());
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_file_that_cannot_be_read_is_SND_RECIPE_010_and_a_failing_one_SND_RECIPE_001()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var missing = Path.Combine(_home.Path, "nope.yaml");
        var invalid = Path.Combine(_home.Path, "RCP-REACH-001.yaml");
        await File.WriteAllTextAsync(invalid, RecipeFixtures.Read("invalid/RCP-REACH-001.yaml"), ct);

        var unread = await Run(app, ct, "register", missing);
        var unreadError = _error.ToString();
        var failed = await Run(app, ct, "register", invalid);

        Assert.Equal((2, 2), (unread, failed));
        Assert.StartsWith("SND-RECIPE-010 ", unreadError, StringComparison.Ordinal);
        Assert.Contains("SND-RECIPE-001 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("RCP-REACH-001: ", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty((await app.Services.GetRequiredService<IRecipeStore>().All(ct)).Value);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task The_registry_hosts_fake_Temporal_so_no_dev_server_races_the_verbs()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);

        _ = Assert.IsType<FakeTemporalServer>(app.Services.GetRequiredService<ITemporalServer>());
    }

    private string Take()
    {
        var text = _out.ToString();
        _out.GetStringBuilder().Clear();
        return text;
    }

    private async Task<string> Shipped(string name, CancellationToken ct)
    {
        var file = Path.Combine(_home.Path, name);
        await File.WriteAllTextAsync(file, ShippedRecipes.Read("sindri/" + name), ct);
        return file;
    }

    private async Task<int> Run(WebApplication app, CancellationToken ct, params string[] args)
    {
        var session = new AgentSession(Signal: null, Terminal: true, Prover: false);
        using var client = new ControlPlaneClient(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json,
            CallerHeaders.For(session, app.Services.GetRequiredService<IHumanTokenStore>(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));
        var desk = new RecipeDesk(client, new StringReader(string.Empty), static _ => Task.FromResult<Error?>(null));
        await using var scope = app.Services.CreateAsyncScope();
        return await RecipeCommandTable.Run(args, scope.ServiceProvider, new Output(_out, _error, SliceHarness.Json), desk, ct);
    }

    /// <summary>A control plane in the temp home, as CliEndToEndTests starts it (Temporal faked): the lease held, this start's token minted and held, its endpoint recorded.</summary>
    private async Task<WebApplication> Started(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);   // the registry verbs don't use Temporal: a real dev server only slows the start under load
        _ = Pipeline.Configure(app);
        Core.Lease.LeaseGrant grant;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        return app;
    }
}
