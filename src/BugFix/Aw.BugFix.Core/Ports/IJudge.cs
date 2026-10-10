using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>judge resolution-check: is the reported problem solved by this change?</summary>
public interface IJudge
{
    /// <summary>Asks judge once and returns the decision it stored for exactly this input.</summary>
    /// <param name="input">The input the helper built from state.</param>
    /// <returns>The answer, or AW-BUGFIX-016 when judge gave no usable verdict (nothing to record; a retry isn't a re-roll).</returns>
    public Result<JudgeAnswer> ResolutionCheck(ResolutionCheckInput input);
}
