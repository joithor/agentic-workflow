using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The sheets of a failure row and a profile row (U3' T6): what the API served about them, and only copies for what the app cannot run.</summary>
[Lane(TestLane.Unit)]
public sealed class RowSheetViewModelTests
{
    [Fact]
    public void Failure_shows_service_last_run_why_and_each_action_with_its_cli()
    {
        var details = FailureDetails.Build(NeedsYouRows.Failure(), "9h");

        Assert.Equal(
            [("Service", "index"), ("Failed", "9h"), ("Why", "SND-SCHED-001: A background service failed"), ("Run again", "Not in the app yet · sindri services run index"), ("Pause", "Not in the app yet · sindri services pause index")],
            details.Select(d => (d.Label, d.Value)));
    }

    [Fact]
    public void Profile_shows_the_changes_the_hash_and_only_the_approve_command_to_copy()
    {
        var entry = NeedsYouRows.Profile(new NeedsYouSubject("profile", "3f1c09"));
        var details = ProfileDetails.Build(entry);

        Assert.Equal(
            [("Changed", "1 key path changed: services.scorer.every"), ("Fingerprint", "3f1c09"), ("To approve, run", "sindri profile approve 3f1c09")],
            details.Select(d => (d.Label, d.Value)));
        Assert.Equal("sindri profile approve 3f1c09", entry.Cli);
    }

    // Review 456-4: the hash and the approve command are bookkeeping; only the plain description stays in the main list.
    [Fact]
    public void Profile_keeps_the_description_up_front_and_the_hash_and_command_in_closed_details()
    {
        var row = new NeedsYouRowViewModel(NeedsYouRows.Profile(new NeedsYouSubject("profile", "3f1c09")), seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));

        Assert.Equal(["Changed"], row.PrimaryDetails.Select(d => d.Label), StringComparer.Ordinal);
        Assert.Equal(["Fingerprint", "To approve, run"], row.MoreDetails.Select(d => d.Label), StringComparer.Ordinal);
        Assert.Equal((true, false, "Details"), (row.HasMoreDetails, row.DetailsOpen, row.MoreHeading));
        Assert.False(row.ShowsCliUpFront);
    }

    [Fact]
    public void A_failure_has_no_closed_details_and_keeps_its_command_up_front()
    {
        var row = new NeedsYouRowViewModel(NeedsYouRows.Failure(), seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));

        Assert.Equal((false, row.Details.Count), (row.HasMoreDetails, row.PrimaryDetails.Count));
        Assert.Equal(row.Cli.Length > 0, row.ShowsCliUpFront);
    }

    [Fact]
    public void A_profile_without_a_subject_shows_no_hash_line()
    {
        var details = ProfileDetails.Build(NeedsYouRows.Profile());

        Assert.DoesNotContain(details, d => d.Label == "Fingerprint");
    }

    [Fact]
    public void A_ready_action_shows_its_command_not_the_not_yet_words()
    {
        var entry = NeedsYouRows.Failure() with { Actions = [new NeedsYouAction("RunService", "Run again", ActionReadiness.Ready, "sindri services run index", RejectLike: false)] };

        Assert.Contains(new SheetDetail("Run again", "sindri services run index", Command: "sindri services run index"), FailureDetails.Build(entry, "1h"));
    }
}
