using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>Runs a ui-evidence script against the running app.</summary>
public interface IUiEvidence
{
    /// <summary>Runs the script once.</summary>
    /// <param name="request">What to run, where, and the build label.</param>
    /// <returns>The run's steps and script hash, or AW-BUGFIX-017 when ui-evidence produced no usable run.</returns>
    public Result<UiRun> Run(UiRunRequest request);
}
