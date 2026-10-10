using Aw.Judge.Core.Config;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>Where the judge's config lives ($AW_STATE_DIR/judge/config.json). Every call returns an error, never throws.</summary>
public interface IJudgeConfigStore
{
    /// <summary>The effective config: the defaults when nothing is stored or what is stored can't be read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The config.</returns>
    public ValueTask<Result<JudgeConfig>> Load(CancellationToken cancellationToken);

    /// <summary>Replaces the stored config.</summary>
    /// <param name="config">The config.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why it couldn't be written.</returns>
    public ValueTask<Result<Unit>> Save(JudgeConfig config, CancellationToken cancellationToken);
}
