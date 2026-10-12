using System.Text.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Client;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// What the engine's integration tests share (engine spec rev 8 §11): a Sindri home with 'workflow.step' under its
/// recipes, the composed host, the pinned dev server with the search attributes serve registers, the lease, and the
/// 'workflow' verb group run in process.
/// </summary>
internal sealed class WorkflowsHarness : IDisposable
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);
    private readonly TempHome _home = new();
    private readonly TempHome _work = new();

    public WorkflowsHarness()
    {
        var recipes = Directory.CreateDirectory(Path.Combine(_home.Path, "recipes")).FullName;
        System.IO.File.Copy(Path.Combine(RepoRoot(), "recipes", "sindri", "workflow.step.yaml"), Path.Combine(recipes, "workflow.step.yaml"));
    }

    public string Home => _home.Path;

    public void Dispose()
    {
        _home.Dispose();
        _work.Dispose();
    }

    /// <summary>The host composed over this home, as every 'sindri' verb composes it.</summary>
    public ServiceProvider Sindri() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider()).BuildServiceProvider();

    /// <summary>The 'temporal' dev server pinned in binaries.lock, with Sindri's namespace and search attributes.</summary>
    public static Task<WorkflowEnvironment> StartDevServer() =>
        WorkflowEnvironment.StartLocalAsync(new WorkflowEnvironmentStartLocalOptions
        {
            Namespace = SindriTemporal.Namespace,
            UI = false,
            DevServerOptions = new DevServerOptions
            {
                ExistingPath = PinnedTemporal.Path(),
                ExtraArgs = [.. SindriTemporal.SearchAttributes.SelectMany(static a => new[] { "--search-attribute", $"{a.Name}={a.Type}" })],
            },
        });

    public static string Target(WorkflowEnvironment env) => env.Client.Connection.Options.TargetHost!;

    public static int PortOf(WorkflowEnvironment env) =>
        int.Parse(Target(env).Split(':')[^1], System.Globalization.CultureInfo.InvariantCulture);

    public static async Task HoldLease(IServiceProvider sindri, CancellationToken ct)
    {
        var grant = await sindri.GetRequiredService<ILease>().Acquire(sindri.GetRequiredService<IProcessProbe>().Self(), ct);
        Assert.False(grant.IsError, grant.IsError ? grant.Error.Message : null);
        sindri.GetRequiredService<HeldLease>().Hold(grant.Value);
    }

    /// <summary>Writes control.json as a running serve would, naming the dev server, and returns its endpoint.</summary>
    public static async Task<TemporalEndpoint> AsServe(IServiceProvider sindri, WorkflowEnvironment env, CancellationToken ct)
    {
        var temporal = new TemporalEndpoint(Pid: Environment.ProcessId, Port: PortOf(env), UiPort: 0);
        var control = new ControlEndpoint(new Uri("http://127.0.0.1:9"), Environment.ProcessId, Epoch: 1) { Temporal = temporal };
        Assert.False((await sindri.GetRequiredService<IEndpointFile>().Write(control, ct)).IsError);
        return temporal;
    }

    /// <summary>Writes <paramref name="yaml"/> as the recipe '&lt;home&gt;/recipes/&lt;id&gt;.yaml' the stub approvals read.</summary>
    public void Recipe(string id, string yaml) => System.IO.File.WriteAllText(Path.Combine(_home.Path, "recipes", id + ".yaml"), yaml);

    /// <summary>The text of the shipped recipe at <paramref name="path"/> (relative to recipes/).</summary>
    public static string Shipped(string path) => System.IO.File.ReadAllText(Path.Combine(RepoRoot(), "recipes", path));

    /// <summary>Stores <paramref name="content"/> as a fact the way the API does before a start: '&lt;home&gt;/workflow/facts/&lt;sha256&gt;'.</summary>
    public FactValue Fact(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var facts = Directory.CreateDirectory(Path.Combine(_home.Path, "workflow", "facts")).FullName;
        System.IO.File.WriteAllBytes(Path.Combine(facts, sha), bytes);
        return new FactValue("fact:sha256:" + sha, sha, HeadSha: null);
    }

    /// <summary>The workflow input for the approved <paramref name="recipe"/> on <paramref name="item"/>, attempt 1, with no inputs.</summary>
    public static async Task<InstanceStart> RecipeStart(IServiceProvider sindri, string recipe, string item, CancellationToken ct)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From(recipe), ct);
        Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        return new InstanceStart(approved.Value.Version, ItemKey.From(item), Attempt: 1, new Dictionary<FactKey, InputFact>(), Tenant: "local", Ring: 0);
    }

    /// <summary>The workflow input for 'workflow.step' on <paramref name="item"/>, attempt 1, with a 'fact:spec' input.</summary>
    public static async Task<InstanceStart> StepStart(IServiceProvider sindri, string item, CancellationToken ct)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From("workflow.step"), ct);
        Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        var spec = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);
        var inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new InputFact("spec.md", spec) };
        return new InstanceStart(approved.Value.Version, ItemKey.From(item), Attempt: 1, inputs, Tenant: "local", Ring: 0);
    }

    public static async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(ITemporalClient client, InstanceStart start) =>
        await client.StartWorkflowAsync((RecipeWorkflow wf) => wf.RunAsync(start), new WorkflowOptions(id: start.WorkflowId().Value, taskQueue: SindriTemporal.TaskQueue));

    /// <summary>Queries 'GetInstance' until <paramref name="holds"/>, failing the test after the patience runs out.</summary>
    public static async Task<InstanceView> Until(WorkflowHandle<RecipeWorkflow> handle, Func<InstanceView, bool> holds)
    {
        var deadline = DateTime.UtcNow + _patience;
        InstanceView? last = null;
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

    /// <summary>Runs 'sindri workflow &lt;args&gt;' in process.</summary>
    public static async Task<(int Exit, string Stdout, string Stderr)> Cli(IServiceProvider services, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new Output(stdout, stderr, services.GetRequiredService<JsonSerializerOptions>());
        var exit = await WorkflowCommandTable.Run(args, services, output, TestContext.Current.CancellationToken);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    /// <summary>A file under the work directory holding <paramref name="content"/>.</summary>
    public string File(string name, string content)
    {
        var file = Path.Combine(_work.Path, name);
        System.IO.File.WriteAllText(file, content);
        return file;
    }

    /// <summary>'workflow list --query', retried while visibility catches up (it is eventually consistent).</summary>
    public static async Task<string> Listed(IServiceProvider sindri, string query, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + _patience;
        var last = (Exit: -1, Stdout: string.Empty, Stderr: string.Empty);
        while (DateTime.UtcNow < deadline)
        {
            last = await Cli(sindri, "list", "--query", query);
            if (last.Exit != 0 || last.Stdout.Length > 0)
            {
                break;
            }

            await Task.Delay(200, ct);
        }

        Assert.Equal((0, string.Empty), (last.Exit, last.Stderr));
        return last.Stdout;
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
