using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine P0 Day 1 (engine spec rev 8, Phases "Phase 0"; §5.1, §5.3, §6.3): the throwaway interpreter runs
/// 'workflow.step' on the pinned dev server (binaries.lock, DevServerOptions.ExistingPath, never a download), its desk
/// Step is an async-completion activity, and 'sindri workflow complete --run 1.1' completes it so the instance moves
/// 'produce -> checks'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RecipeWorkflowTests : IDisposable
{
    private const string Address = "T-1/step/1";
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);
    private readonly TempHome _home = new();
    private readonly TempHome _work = new();

    public RecipeWorkflowTests()
    {
        var recipes = Directory.CreateDirectory(Path.Combine(_home.Path, "recipes")).FullName;
        File.Copy(Path.Combine(RepoRoot(), "recipes", "sindri", "workflow.step.yaml"), Path.Combine(recipes, "workflow.step.yaml"));
    }

    public void Dispose()
    {
        _home.Dispose();
        _work.Dispose();
    }

    [Fact]
    public async Task Complete_run_1_1_moves_workflow_step_from_produce_to_checks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await StartDevServer();
        await using var sindri = Sindri();
        using var worker = new TemporalWorker(env.Client, SindriWorker.Options(sindri.GetRequiredService<RunStepActivities>()));

        await worker.ExecuteAsync(async () =>
        {
            var handle = await StartStep(env.Client);
            _ = await Until(handle, static s => IsOpen(s, "produce", "1.1"));
            Assert.True(File.Exists(TaskFile("1.1")), "RunStep didn't record its task token under run 1.1");

            var (exit, stdout, stderr) = await Cli(sindri, "complete", Address, "--run", "1.1", "--outcome", "done",
                "--output", "fact:output=" + Output("the produced output"), "--temporal", Target(env));

            Assert.Equal((0, $"{Address} run 1.1: done -> checks\n", string.Empty), (exit, stdout, stderr));
            var checks = await handle.QueryAsync(static wf => wf.GetInstance());
            Assert.Equal(("checks", "2.1"), (checks.State, checks.Run));
            Assert.True(File.Exists(TaskFile("2.1")), "the checks Step didn't record its task token under run 2.1");

            var (againExit, _, againStderr) = await Cli(sindri, "complete", Address, "--run", "1.1", "--outcome", "done", "--temporal", Target(env));
            Assert.Equal(2, againExit);
            Assert.Contains("SND-WF-005", againStderr, StringComparison.Ordinal);

            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>())
                .ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, ct);
            Assert.Null(replayed.ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task A_desk_run_completed_while_no_worker_runs_moves_on_when_a_new_worker_starts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await StartDevServer();
        await using (var first = Sindri())
        {
            using var worker = new TemporalWorker(env.Client, SindriWorker.Options(first.GetRequiredService<RunStepActivities>()));
            await worker.ExecuteAsync(async () => { _ = await Until(await StartStep(env.Client), static s => IsOpen(s, "produce", "1.1")); }, ct);
        }

        await using var second = Sindri();
        var run = await Cli(second, "complete", Address, "--run", "1.1", "--outcome", "done",
            "--output", "fact:output=" + Output("written while the worker was down"), "--temporal", Target(env), "--no-wait");
        Assert.Equal((0, $"{Address} run 1.1: done -> sent; the instance moves on when a worker runs\n", string.Empty), run);

        using var restarted = new TemporalWorker(env.Client, SindriWorker.Options(second.GetRequiredService<RunStepActivities>()));
        await restarted.ExecuteAsync(async () =>
        {
            var handle = env.Client.GetWorkflowHandle<RecipeWorkflow>(Address);
            var checks = await Until(handle, static s => string.Equals(s.State, "checks", StringComparison.Ordinal));
            Assert.Equal("2.1", checks.Run);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private static bool IsOpen(InstanceSnapshot snapshot, string state, string run) =>
        string.Equals(snapshot.State, state, StringComparison.Ordinal) && string.Equals(snapshot.Run, run, StringComparison.Ordinal);

    private static async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> StartStep(ITemporalClient client)
    {
        var spec = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);
        var run = new RecipeRun(Address, new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:spec"] = spec });
        return await client.StartWorkflowAsync((RecipeWorkflow wf) => wf.RunAsync(run), new WorkflowOptions(id: Address, taskQueue: SindriTemporal.TaskQueue));
    }

    private static async Task<InstanceSnapshot> Until(WorkflowHandle<RecipeWorkflow> handle, Func<InstanceSnapshot, bool> holds)
    {
        var deadline = DateTime.UtcNow + _patience;
        InstanceSnapshot? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = await handle.QueryAsync(static wf => wf.GetInstance());
            if (holds(last))
            {
                return last;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"the instance never got there; last seen {last}");
        return last;
    }

    private static Task<WorkflowEnvironment> StartDevServer() =>
        WorkflowEnvironment.StartLocalAsync(new WorkflowEnvironmentStartLocalOptions
        {
            Namespace = SindriTemporal.Namespace,
            UI = false,
            DevServerOptions = new DevServerOptions { ExistingPath = PinnedTemporal() },
        });

    private static string Target(WorkflowEnvironment env) => env.Client.Connection.Options.TargetHost!;

    private ServiceProvider Sindri() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider()).BuildServiceProvider();

    private static async Task<(int Exit, string Stdout, string Stderr)> Cli(IServiceProvider services, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new Output(stdout, stderr, services.GetRequiredService<JsonSerializerOptions>());
        var exit = await WorkflowCommandTable.Run(args, services, output, TestContext.Current.CancellationToken);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private string Output(string content)
    {
        var file = Path.Combine(_work.Path, Path.GetRandomFileName());
        File.WriteAllText(file, content);
        return file;
    }

    private string TaskFile(string run) => Path.Combine(_home.Path, "workflow", "tasks", Address.Replace('/', '~'), run);

    /// <summary>The 'temporal' CLI pinned in binaries.lock, where scripts/binaries-lock.sh fetch installs it.</summary>
    private static string PinnedTemporal()
    {
        var row = File.ReadLines(Path.Combine(RepoRoot(), "binaries.lock"))
            .Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .First(f => f.Length == 6 && f[0] == "temporal");
        var stateDir = Environment.GetEnvironmentVariable("AW_STATE_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var path = Path.Combine(stateDir, "cache", "bin", $"temporal-{row[1]}", "temporal");
        if (!File.Exists(path))
        {
            Assert.Fail($"the pinned temporal CLI {row[1]} is not installed at {path}: run scripts/binaries-lock.sh fetch temporal");
        }

        return path;
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
