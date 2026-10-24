using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure.Evidence;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Setup;

/// <summary>Doctor's 'setup:ui-evidence' line (cutover spec §3.1): the binary next to sindri, or where AW_UI_EVIDENCE_BIN points, answering 'version'; a warning, never a failure, when it is missing.</summary>
[Lane(TestLane.Unit)]
public sealed class UiEvidenceDoctorChecksTests : IDisposable
{
    private readonly TempHome _home = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    private string Touch(string name)
    {
        var path = Path.Combine(_home.Path, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static async Task<DoctorCheck> Check(Func<string, string?> environment, string? sindri, FakeToolRunner runner) =>
        Assert.Single(await new UiEvidenceDoctorChecks(environment, sindri, runner).Run(Token));

    [Fact]
    public async Task A_binary_next_to_sindri_that_answers_version_is_ok_and_shows_the_stamp()
    {
        _ = Touch("ui-evidence");
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "ui-evidence 1.2.3\n"));

        var check = await Check(static _ => null, Path.Combine(_home.Path, "sindri"), runner);

        Assert.Equal(new DoctorCheck("setup:ui-evidence", CheckState.Ok, "ui-evidence 1.2.3", Fix: null), check);
        Assert.Equal((Path.Combine(_home.Path, "ui-evidence"), ["version"]), (runner.Calls[0].FileName, runner.Calls[0].Arguments));
    }

    [Fact]
    public async Task AW_UI_EVIDENCE_BIN_wins_over_the_neighbour()
    {
        var chosen = Touch("elsewhere");
        _ = Touch("ui-evidence");
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "v\n"));

        _ = await Check(name => name == "AW_UI_EVIDENCE_BIN" ? chosen : null, Path.Combine(_home.Path, "sindri"), runner);

        Assert.Equal(chosen, runner.Calls[0].FileName);
    }

    [Fact]
    public async Task A_missing_binary_is_a_warning_that_names_setup_sh_and_runs_nothing()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "x"));

        var check = await Check(static _ => null, Path.Combine(_home.Path, "sindri"), runner);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("./setup.sh", check.Fix, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData("Exited", 2)]
    [InlineData("TimedOut", -1)]
    [InlineData("Missing", -1)]
    public async Task A_binary_that_does_not_answer_version_is_a_warning(string outcome, int exit)
    {
        _ = Touch("ui-evidence");

        var check = await Check(static _ => null, Path.Combine(_home.Path, "sindri"), new FakeToolRunner(new ToolRun(Enum.Parse<ToolOutcome>(outcome), exit, "")));

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("version", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_known_sindri_path_and_no_variable_it_is_a_warning()
    {
        var check = await Check(static _ => null, sindri: null, new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "")));

        Assert.Equal(CheckState.Warn, check.State);
    }
}
