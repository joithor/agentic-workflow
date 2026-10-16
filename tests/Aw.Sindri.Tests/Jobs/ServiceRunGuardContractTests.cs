using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>Binds the service run guard contract to the real guard (API P6 unit D). Red until the guard is implemented.</summary>
[Lane(TestLane.Unit)]
public sealed class ServiceRunGuardContractTests : ServiceRunGuardContract
{
    protected override ServiceRunGuard Create() => new();
}
