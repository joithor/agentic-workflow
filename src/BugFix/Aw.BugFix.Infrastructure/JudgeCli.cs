using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The judge port over the judge binary (cutover spec §4.3 b): judge resolution-check with the input JSON on stdin, then
/// judge why &lt;id&gt; for the stored input digest, each bounded to 5 minutes. The binary is AW_JUDGE_BIN else PATH, and it
/// reads the judge state under AW_JUDGE_STATE_DIR else AW_STATE_DIR. The check kind ui goes out as ui-evidence, and the
/// answer names the judge's 16-hex input digest.
/// Contract stub: unit 2.1 fills the body and registers it in place of <see cref="UnavailableJudge"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0182:Avoid unused internal types", Justification = "Contract stub: cutover unit 2.1 registers it in place of the Unavailable adapter")]
internal sealed class JudgeCli(Func<string, string?> environment) : IJudge
{
    /// <summary>Gets the environment the binary, its state directory and PATH are read from.</summary>
    internal Func<string, string?> Environment { get; } = environment;

    public Result<JudgeAnswer> ResolutionCheck(JudgeQuestion question) =>
        throw new NotSupportedException("judge resolution-check isn't implemented yet (cutover unit 2.1)");
}
