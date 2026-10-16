using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The served "recommended" option is the one that is styled primary and the one A runs (review run 1, K8 / corr-6).</summary>
[Lane(TestLane.Unit)]
public sealed class RecommendedOptionTests
{
    private static NeedsYouEntry DeclineRecommended()
    {
        var row = NeedsYouRows.Decision();
        return row with
        {
            Actions =
            [
                row.Actions[0] with { Recommended = false },
                row.Actions[1] with { Recommended = true },
            ],
        };
    }

    [Fact]
    public void A_runs_the_recommended_option_even_when_it_is_listed_second()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new StillMotion(), clock);
        list.Update(new NeedsYouFeed([DeclineRecommended()], ""));
        list.Select("decision:DEC-1");
        list.OpenSheetOn("decision:DEC-1");
        var asked = new List<string>();
        list.ActionRequested += (_, e) => asked.Add(e.Action.OptionId);
        clock.Advance(NeedsYouViewModel.KeyQuiet);

        Assert.True(list.Press(NeedsYouKey.A));

        Assert.Equal(["decline"], asked);
    }

    [Fact]
    public void The_mapper_marks_the_option_the_decision_recommends()
    {
        var served = AwJson.Deserialize<DecisionList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("decisions", "pending")), SindriApiClient.Json).Value;
        var decision = served.Decisions[0] with { Recommended = served.Decisions[0].Options[1].Id };
        var list = AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("needs-you", "fixture")), SindriApiClient.Json).Value;
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
        var row = list.Rows[0] with { Subject = list.Rows[0].Subject! with { Id = decision.Id } };

        var feed = NeedsYouMapper.Map(list with { Rows = [row] }, catalog, served with { Decisions = [decision] }, new HomeCounts(0, 0, 0));

        Assert.Equal([false, true], feed.Rows[0].Actions.Select(a => a.Recommended));
    }

    [Fact]
    public void Only_the_recommended_option_of_a_decision_row_is_primary()
    {
        var row = new NeedsYouRowViewModel(DeclineRecommended(), seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));

        var primary = row.Controls.Where(c => c.IsPrimary).Select(c => c.Action.OptionId);

        Assert.Equal(["decline"], primary, StringComparer.Ordinal);
    }
}
