using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Notifications;

/// <summary>The notifications file in a temp home, with a fake OS notification centre.</summary>
[Lane(TestLane.Integration)]
public sealed class FileDeskNoticesContractTests : DeskNoticesContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IDeskNotices Create() =>
        new FileDeskNotices(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeOsNotifier(), SliceHarness.Json);
}
