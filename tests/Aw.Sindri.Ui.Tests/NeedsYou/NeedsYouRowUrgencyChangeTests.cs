using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A row whose urgency changes keeps its word "new" right (UI spec §11.4: the word follows an unseen pulse row).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRowUrgencyChangeTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_row_replaced_Colour_to_Pulse_shows_the_word_new_and_back()
    {
        var panel = new NeedsYouSeenViewModel(new Preferences(_store), _os, _clock);
        await panel.Restore(Token);
        panel.Update([new NeedsYouEntry("r", NeedsYouUrgency.Colour)]);
        var row = panel.Rows[0];
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.Equal((false, ""), (row.HasNewWord, row.NewWord));

        panel.Update([new NeedsYouEntry("r", NeedsYouUrgency.Pulse)]);

        Assert.Equal((true, "new"), (row.HasNewWord, row.NewWord));
        Assert.Contains(nameof(row.HasNewWord), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(row.NewWord), raised, StringComparer.Ordinal);

        raised.Clear();
        panel.Update([new NeedsYouEntry("r", NeedsYouUrgency.Colour)]);

        Assert.Equal((false, ""), (row.HasNewWord, row.NewWord));
        Assert.Contains(nameof(row.HasNewWord), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(row.NewWord), raised, StringComparer.Ordinal);
    }
}
