using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// UI spec E-17: every API golden reads through the app's own JSON context, so a wire change in Sindri fails this unit
/// lane, not a prover run. A golden is a record the app reads, a ProblemDetails refusal, or a route the app never calls.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GoldenContractTests
{
    // The routes the app reads, by slug, and how; a new slug fails until it is placed here or in _notRead.
    private static readonly Dictionary<string, Func<byte[], Result<object>>> _read = new(StringComparer.Ordinal)
    {
        ["ping"] = As<PingView>,
        ["status"] = As<StatusView>,
        ["events"] = As<EventView>,
        ["commands"] = As<CommandList>,
        ["services"] = As<ServiceList>,
        ["views-rebuild"] = As<CommandAcceptedView>,
    };

    // Routes the app never calls: stop is Health's (U4), and profile approve is protected (the app shows the CLI, §6.5).
    private static readonly HashSet<string> _notRead = new(StringComparer.Ordinal) { "stop", "profile-approve" };

    [Fact]
    public void Every_golden_reads_through_the_apps_json_context_or_is_a_refusal()
    {
        var goldens = GoldenFiles.All().ToList();
        Assert.Contains(goldens, g => g.Kind == "Api");
        Assert.Contains(goldens, g => g.Kind == "SpecShaped");

        foreach (var (kind, slug, scenario, body) in goldens)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            if (Problem(bytes) is { } problem)
            {
                Assert.True(problem.Title.StartsWith("SND-", StringComparison.Ordinal), $"{kind}/{slug}/{scenario}: {problem.Title}");
                continue;
            }

            if (_notRead.Contains(slug))
            {
                continue;
            }

            Assert.True(_read.TryGetValue(slug, out var read), $"{kind}/{slug}/{scenario}: a route the app doesn't place (read it or list it as not read)");
            var value = read(bytes);
            Assert.True(value.IsSuccess, $"{kind}/{slug}/{scenario}: {(value.IsError ? value.Error.Message : string.Empty)}");
        }
    }

    [Fact]
    public void The_p1_catalog_carries_every_field_the_control_renderer_reads()
    {
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;

        var rebuild = Assert.Single(catalog.Rows, r => r.Action == "RebuildViews");
        Assert.Equal(("desk", "ready", "sindri views rebuild", false, false), (rebuild.Class, rebuild.Ui, rebuild.Cli, rebuild.Destructive, rebuild.ReadsViews));
        var stop = Assert.Single(catalog.Rows, r => r.Action == "StopProcess");
        Assert.Equal(("ready", true), (stop.Ui, stop.Destructive));
        Assert.Equal("cli-only:typed-confirmation", Assert.Single(catalog.Rows, r => r.Action == "ApproveProfile").Ui);
    }

    private static ProblemView? Problem(byte[] body)
    {
        var problem = AwJson.Deserialize<ProblemView>(body, SindriApiClient.Json);
        return problem.IsSuccess ? problem.Value : null;
    }

    private static Result<object> As<T>(byte[] body)
        where T : class
    {
        var value = AwJson.Deserialize<T>(body, SindriApiClient.Json);
        return value.IsSuccess ? value.Value : value.Error;
    }
}
