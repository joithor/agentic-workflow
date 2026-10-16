using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>
/// Binds the job runtime contract to the real runtime (API P6 unit C). The cases are named in <see cref="JobRuntimeContract"/>.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JobRuntimeContractTests : JobRuntimeContract
{
    protected override async ValueTask<IJobRuntimeSubject> Create()
    {
        var runtime = new RealJobRuntime();
        await runtime.Open();
        return runtime;
    }
}
