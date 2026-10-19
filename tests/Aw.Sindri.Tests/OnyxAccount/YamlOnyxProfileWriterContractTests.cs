using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

[Lane(TestLane.Integration)]
public sealed class YamlOnyxProfileWriterContractTests : OnyxProfileWriterContract, IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-profile-contract-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    protected override IOnyxProfileWriter Create() => new YamlOnyxProfileWriter(new SindriSettings(_dir, Path.Combine(_dir, "profile", "profile.yaml")));
}
