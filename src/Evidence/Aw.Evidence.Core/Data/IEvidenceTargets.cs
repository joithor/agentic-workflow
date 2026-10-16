using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>The targets config of the repo under test: where its programs live, which may run and which variables they inherit.</summary>
public interface IEvidenceTargets
{
    /// <summary>Loads the config.</summary>
    /// <param name="repo">The repository (absolute); its .agentic-workflow/evidence.json is read.</param>
    /// <param name="configPath">A file (absolute) read instead of the repo's, when given.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The targets (empty when the repo has no file), AW-EVIDENCE-010 for a missing --config file, or AW-EVIDENCE-022 for an invalid one.</returns>
    public ValueTask<Result<ResolvedTargets>> LoadAsync(string repo, string? configPath, CancellationToken cancellationToken);
}
