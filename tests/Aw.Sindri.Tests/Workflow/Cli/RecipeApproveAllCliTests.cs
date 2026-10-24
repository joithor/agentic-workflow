using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ApproveAll;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri recipe approve-all' against the real host (setup-once spec §5.3, §7.2): the plan is printed in order, the job starts
/// with no typed phrase, a session may start it too (owner decision q93), and an empty plan says so and exits 2.
/// </summary>
public sealed class RecipeApproveAllCliTests : IDisposable
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Human_sees_the_plan_in_order_and_gets_the_started_job_with_no_phrase()
    {
        var ct = TestContext.Current.CancellationToken;
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        await using var host = await ApproveAllScenarios.Start(shipped, ct);
        await ViewScenarios.Approve(host, ct);
        var digest = (await shipped.Plan(ct)).Value.Digest;

        var code = await (await Command(host, human: true, $"approve-all {digest}", ct)).Run(json: false, ct);

        var text = _out.ToString();
        Assert.True(code == 0, _error.ToString());
        Assert.Contains($"approving 2 recipes, children first (digest {digest}):\n", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("workflow.step version 2", StringComparison.Ordinal) < text.IndexOf("workflow.item version 3", StringComparison.Ordinal));
        Assert.DoesNotContain("Type '", text, StringComparison.Ordinal);
        Assert.Contains("approve-all started as job JOB-", text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_session_starts_the_job_too()
    {
        var ct = TestContext.Current.CancellationToken;
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        await using var host = await ApproveAllScenarios.Start(shipped, ct);
        await ViewScenarios.Approve(host, ct);

        var code = await (await Command(host, human: false, string.Empty, ct)).Run(json: false, ct);

        Assert.True(code == 0, _error.ToString());
        Assert.Contains("approve-all started as job JOB-", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task An_empty_plan_says_so_and_exits_2_without_a_challenge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ApproveAllScenarios.Start(new FakeShippedRecipes([]), ct);
        await ViewScenarios.Approve(host, ct);

        var code = await (await Command(host, human: true, string.Empty, ct)).Run(json: false, ct);

        Assert.Equal(2, code);
        Assert.Contains("Every recipe is approved.", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Type '", _out.ToString(), StringComparison.Ordinal);
    }

    private async Task<ApproveAllCommand> Command(GoldenHost host, bool human, string typed, CancellationToken ct)
    {
        var store = host.App.Services.GetRequiredService<IHumanTokenStore>();
        host.App.Services.GetRequiredService<HeldHumanToken>().Hold((await store.Mint(ct)).Value);   // the CLI reads the stored token, so the host holds that one
        var session = new AgentSession(Terminal: true, Prover: false);
        var endpoint = new ControlEndpoint(new Uri(host.App.Urls.First()), GoldenHost.Pid, 1);
        var client = new ControlPlaneClient(FakeControlPlaneProbe.For(endpoint, new Ping(GoldenHost.Pid, 1)), SliceHarness.Json,
            CallerHeaders.For(session, human ? store : new InMemoryHumanTokenStore(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));
        host.Own(client);
        var desk = new RecipeDesk(client, new StringReader(typed + "\n"), static _ => Task.FromResult<Error?>(null));
        return new ApproveAllCommand(desk, new Output(_out, _error, SliceHarness.Json));
    }
}
