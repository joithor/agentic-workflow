using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="IUiEvidence"/> contract, for the scripted fake today and the Evidence CLI adapter once it exists.</summary>
public abstract class UiEvidenceContract
{
    /// <summary>A run reports each step and the hash of the script it executed.</summary>
    [Fact]
    public void A_run_reports_its_steps_and_the_script_hash()
    {
        var (ui, request, scriptSha) = Arrange();

        var run = ui.Run(request);

        Expect.That(run.Value.Steps.Any(), "NotEmpty(run.Value.Steps)");
        Expect.Equal(scriptSha, run.Value.ScriptSha256, "Equal(scriptSha, run.Value.ScriptSha256)");
    }

    /// <summary>No usable run is AW-BUGFIX-017.</summary>
    [Fact]
    public void No_usable_run_is_AW_BUGFIX_017()
    {
        var (_, request, _) = Arrange();

        Expect.Equal(BugFixErrors.UiEvidenceUnavailable.Code, Failing().Run(request).Error.Code, "Equal(BugFixErrors.UiEvidenceUnavailable.Code, Failing().Run(request).Error.Code)");
    }

    /// <summary>A working runner, a request for an existing script, and the script's SHA-256.</summary>
    /// <returns>The three.</returns>
    protected abstract (IUiEvidence Ui, UiRunRequest Request, string ScriptSha) Arrange();

    /// <summary>A runner that produces no usable run.</summary>
    /// <returns>The port.</returns>
    protected abstract IUiEvidence Failing();
}
