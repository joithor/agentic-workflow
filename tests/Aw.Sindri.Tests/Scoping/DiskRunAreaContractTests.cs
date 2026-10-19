using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The disk run area honours the run-area contract.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskRunAreaContractTests : RunAreaContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IRunArea Create() => new DiskRunArea(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")));
}
