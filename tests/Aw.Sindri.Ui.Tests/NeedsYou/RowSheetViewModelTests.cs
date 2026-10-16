using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The sheets of a failure row and a profile row (U3' T6): what the API served about them, and only copies for what the app cannot run.</summary>
[Lane(TestLane.Unit)]
public sealed class RowSheetViewModelTests
{
    [Fact]
    public void Failure_shows_service_last_run_why_and_each_action_with_its_cli()
    {
        var sheet = new FailureSheetViewModel(NeedsYouRows.Failure(), "9h");

        Assert.Equal(
            [("Service", "index"), ("Failed", "9h"), ("Why", "failure · index"), ("Run again", "Not in the app yet · sindri services run index"), ("Pause", "Not in the app yet · sindri services pause index")],
            sheet.Details.Select(d => (d.Label, d.Value)));
    }

    [Fact]
    public void Profile_shows_the_changes_the_hash_and_only_the_approve_command_to_copy()
    {
        var sheet = new ProfileSheetViewModel(NeedsYouRows.Profile(new NeedsYouSubject("profile", "3f1c09")));

        Assert.Equal(
            [("Changed", "profile · drifted"), ("Profile hash", "3f1c09"), ("Approve with", "sindri profile approve 3f1c09")],
            sheet.Details.Select(d => (d.Label, d.Value)));
        Assert.Equal("sindri profile approve 3f1c09", sheet.ApproveCli);
    }

    [Fact]
    public void A_profile_without_a_subject_shows_no_hash_line()
    {
        var sheet = new ProfileSheetViewModel(NeedsYouRows.Profile());

        Assert.DoesNotContain(sheet.Details, d => d.Label == "Profile hash");
    }

    [Fact]
    public void A_ready_action_shows_its_command_not_the_not_yet_words()
    {
        var entry = NeedsYouRows.Failure() with { Actions = [new NeedsYouAction("RunService", "Run again", ActionReadiness.Ready, "sindri services run index", RejectLike: false)] };

        Assert.Contains(new SheetDetail("Run again", "sindri services run index"), new FailureSheetViewModel(entry, "1h").Details);
    }
}
