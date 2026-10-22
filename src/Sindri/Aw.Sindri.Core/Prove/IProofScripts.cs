using Aw.Kernel;

namespace Aw.Sindri.Core.Prove;

/// <summary>Runs a recorded evidence script in a worktree (the re-proof child's 'step.prove.rerun-scripts', PB spec §8).</summary>
public interface IProofScripts
{
    /// <summary>Runs one script with the worktree as its working directory.</summary>
    /// <param name="worktree">The phase worktree, checked out at the head.</param>
    /// <param name="script">The script's path, relative to the worktree.</param>
    /// <param name="cancellationToken">Cancels the run: the script is killed.</param>
    /// <returns>How it ended; an error when the script could not be started.</returns>
    public ValueTask<Result<ProofScriptRun>> Run(string worktree, string script, CancellationToken cancellationToken);
}
