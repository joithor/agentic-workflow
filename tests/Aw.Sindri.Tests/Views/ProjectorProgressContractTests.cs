using Aw.Kernel.Testing;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

[Lane(TestLane.Unit)]
public sealed class ProjectorProgressContractTests : ProjectorStatusContract
{
    protected override ProjectorSubject Create(TimeProvider clock)
    {
        var progress = new ProjectorProgress(clock);
        return new ProjectorSubject(progress, progress, progress.Publish);
    }
}
