using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The home's nonce table (engine spec rev 8 §5.2): the contract, on owner-only files that outlive the CLI process.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeCommandNoncesContractTests : CommandNoncesContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ICommandNonces Create(FakeTimeProvider clock) => new HomeCommandNonces(new WorkflowsHome(_home.Path), clock);

    [Fact]
    public async Task A_claim_made_by_one_process_refuses_another_command_in_the_next()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider();
        var nonce = Create(clock).Issue();
        _ = await Create(clock).Claim(nonce, "subject-1", ct);

        var other = await Create(clock).Claim(nonce, "subject-2", ct);

        Assert.Equal("SND-WF-009", other.IsError ? other.Error.Code : "accepted");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, ModeOf(Path.Combine(_home.Path, "workflow", "nonces", nonce.Value)));
    }

    private static UnixFileMode? ModeOf(string path) => OperatingSystem.IsWindows() ? null : File.GetUnixFileMode(path);
}
