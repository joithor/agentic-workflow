using Aw.Kernel;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Ports;

/// <summary>The Claude sessions running on this machine.</summary>
public interface ISessionDirectory
{
    /// <summary>Lists sessions whose process is still alive. A record that can't be read is skipped.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The live sessions; none when the directory doesn't exist.</returns>
    public ValueTask<Result<IReadOnlyList<LiveSession>>> Live(CancellationToken cancellationToken);
}
