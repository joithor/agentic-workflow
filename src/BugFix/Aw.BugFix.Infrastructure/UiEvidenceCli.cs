using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The ui-evidence port over the ui-evidence binary (cutover spec §4.3 a): ui-evidence run &lt;script&gt; &lt;run-dir&gt; --app-build &lt;commit&gt;,
/// found by AW_UI_EVIDENCE_BIN else PATH. Exit 0 is a passing run, 2 a failed check, 4 a broken one, anything else AW-BUGFIX-017.
/// Contract stub: unit 2.1 fills the body and registers it in place of <see cref="UnavailableUiEvidence"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0182:Avoid unused internal types", Justification = "Contract stub: cutover unit 2.1 registers it in place of the Unavailable adapter")]
internal sealed class UiEvidenceCli(Func<string, string?> environment) : IUiEvidence
{
    /// <summary>Gets the environment the binary, its state directory and PATH are read from.</summary>
    internal Func<string, string?> Environment { get; } = environment;

    public Result<UiRun> Run(UiRunRequest request) =>
        throw new NotSupportedException("ui-evidence run isn't implemented yet (cutover unit 2.1)");
}
