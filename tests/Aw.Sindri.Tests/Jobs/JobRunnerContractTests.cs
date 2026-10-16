using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>Binds the runner contract to the real runner (API P6 review T8). The cases are named in <see cref="JobRunnerContract"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRunnerContractTests : JobRunnerContract
{
    protected override async ValueTask<IJobRunnerSubject> Create() => await RealJobRunnerSubject.Open();
}
