using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The judge port until the C# judge CLI publishes its contract (docs/csharp/judge.md, the Judge packet): bugfix-state
/// doesn't guess a contract, so every resolution check is AW-BUGFIX-016 and nothing is recorded.
/// </summary>
internal sealed class UnavailableJudge : IJudge
{
    public Result<JudgeAnswer> ResolutionCheck(ResolutionCheckInput input) =>
        BugFixErrors.JudgeUnavailable.With(
            "judge resolution-check isn't wired into the C# bugfix-state yet (it waits on the C# judge CLI contract, docs/csharp/judge.md); no verdict recorded");
}
