using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// The ui-evidence port until the Evidence package is ported and publishes its CLI contract: bugfix-state doesn't guess
/// a contract, so every run is AW-BUGFIX-017 and nothing is registered.
/// </summary>
internal sealed class UnavailableUiEvidence : IUiEvidence
{
    public Result<UiRun> Run(UiRunRequest request) =>
        BugFixErrors.UiEvidenceUnavailable.With(
            "ui-evidence isn't wired into the C# bugfix-state yet (it waits on the C# Evidence CLI contract); no run registered");
}
