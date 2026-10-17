using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>Binds the runner contract to the inline fake the unit tests run on (API P6 review T8).</summary>
[Lane(TestLane.Unit)]
public sealed class InlineJobRunnerContractTests : JobRunnerContract
{
    protected override async ValueTask<IJobRunnerSubject> Create() => await InlineJobRunnerSubject.Open();
}
