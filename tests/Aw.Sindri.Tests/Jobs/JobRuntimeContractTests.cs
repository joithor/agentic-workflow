using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>
/// Binds the job runtime contract to the real runtime (API P6 unit C). Red until the start service, the runner and its writer exist: the subject
/// cannot be created yet. The cases are named in <see cref="JobRuntimeContract"/>.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JobRuntimeContractTests : JobRuntimeContract
{
    protected override ValueTask<IJobRuntimeSubject> Create() => throw new NotSupportedException("red until API P6 unit C: the real job runtime");
}
