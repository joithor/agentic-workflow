using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// Red on purpose until U7 (API P10a): the real launcher is a stub whose members throw. The contract itself is proven green
/// against <see cref="FakeLauncherContractTests"/>. U7 swaps <see cref="Create"/> for the composed launcher over the fake host and viewer.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LauncherContractTests : LauncherContract
{
    protected override ILauncher Create() => new Launcher();

    protected override int SessionCount() => 0;
}
