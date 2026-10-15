using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IEvidenceTargets that answers with the targets it was given (no config by default), recording what it was asked for.</summary>
public sealed class FakeEvidenceTargets : IEvidenceTargets
{
    /// <summary>The targets every load answers with.</summary>
    public ResolvedTargets Targets { get; set; } = ResolvedTargets.Empty;

    /// <summary>The (repo, configPath) of every load.</summary>
    public IList<(string Repo, string? ConfigPath)> Loads { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<ResolvedTargets>> LoadAsync(string repo, string? configPath, CancellationToken cancellationToken)
    {
        Loads.Add((repo, configPath));
        return ValueTask.FromResult<Result<ResolvedTargets>>(Targets);
    }
}
