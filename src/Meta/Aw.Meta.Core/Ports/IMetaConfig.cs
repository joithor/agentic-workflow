using Aw.Kernel;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Ports;

/// <summary>The team config.</summary>
public interface IMetaConfig
{
    /// <summary>Reads the config. Fields it doesn't know are ignored.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The config, AW-META-002 when there is none, or AW-META-003 when it is invalid.</returns>
    public ValueTask<Result<TeamConfig>> Load(CancellationToken cancellationToken);

    /// <summary>Writes the config, replacing any earlier one.</summary>
    /// <param name="config">The config.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Save(TeamConfig config, CancellationToken cancellationToken);
}
