using Aw.Kernel;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The disk side of <see cref="ILaunchFiles"/> (API spec §8.1, launcher L-9): the secret hashes under <c lang="text">launch/secrets/</c> and the flat files under <c lang="text">launches/</c>.</summary>
/// <param name="options">Where the files live.</param>
internal sealed class LaunchFileStore(LauncherOptions options) : ILaunchFiles
{
    public Result<Unit> Verify(LaunchId launch, LaunchSecret secret)
    {
        _ = (options, launch, secret);
        throw new NotSupportedException("LaunchFileStore: make LaunchFileStoreTests pass");
    }

    public void Forget(LaunchId launch)
    {
        _ = (options, launch);
        throw new NotSupportedException("LaunchFileStore: make LaunchFileStoreTests pass");
    }

    public int SweepStale(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        _ = (options, now, olderThan, live);
        throw new NotSupportedException("LaunchFileStore: make LaunchFileStoreTests pass");
    }
}
