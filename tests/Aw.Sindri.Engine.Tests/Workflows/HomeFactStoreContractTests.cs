using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The home's fact files (engine spec rev 8.1 §5.3, §6.1): the contract, and each file owner-only (0600) under '&lt;home&gt;/workflow/facts'.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeFactStoreContractTests : FactStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IFactStore Create() => new HomeFactStore(new WorkflowsHome(_home.Path));

    [Fact]
    public async Task A_stored_fact_is_an_owner_only_file_named_by_its_sha256()
    {
        var stored = await Create().Put(Encoding.UTF8.GetBytes("the output"), TestContext.Current.CancellationToken);

        var file = Path.Combine(_home.Path, "workflow", "facts", stored.Value.Sha256);
        Assert.Equal("the output", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, ModeOf(file));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, ModeOf(Path.GetDirectoryName(file)!)); // why: a file path has a directory
    }

    [Fact]
    public async Task An_empty_file_a_crash_left_under_a_facts_name_is_rewritten()
    {
        var content = Encoding.UTF8.GetBytes("the output");
        var stored = await Create().Put(content, TestContext.Current.CancellationToken);
        var file = Path.Combine(_home.Path, "workflow", "facts", stored.Value.Sha256);
        await File.WriteAllBytesAsync(file, [], TestContext.Current.CancellationToken);

        _ = await Create().Put(content, TestContext.Current.CancellationToken);

        Assert.Equal("the output", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
    }

    private static UnixFileMode? ModeOf(string path) => OperatingSystem.IsWindows() ? null : File.GetUnixFileMode(path);
}
