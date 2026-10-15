using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The G chord's one second (UI spec §8): it ends by the clock, and a key after it is a fresh key.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellChordTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ShellViewModel Shell() => new(new InMemoryEndpointRecords(), new FakeSindriApi(), _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());

    [Fact]
    public void The_chord_hint_hides_when_the_second_passes_without_a_key()
    {
        var shell = Shell();

        _ = shell.Press("G");
        Assert.Equal("G …", shell.ChordHint);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal("G …", shell.ChordHint);
        _clock.Advance(TimeSpan.FromMilliseconds(600));

        Assert.Equal(string.Empty, shell.ChordHint);
    }

    [Fact]
    public void A_g_after_an_expired_chord_starts_a_new_chord()
    {
        var shell = Shell();
        shell.GoTo(PageId.Home);

        _ = shell.Press("G");
        _clock.Advance(TimeSpan.FromSeconds(2));
        _ = shell.Press("G");
        Assert.Equal("G …", shell.ChordHint);
        _ = shell.Press("Q");

        Assert.Equal((PageId.Queue, QueueFilter.All, string.Empty), (shell.CurrentPage, shell.Queue.Showing, shell.ChordHint));
    }

    [Fact]
    public void A_second_key_inside_the_second_still_completes_the_chord()
    {
        var shell = Shell();
        shell.GoTo(PageId.Home);

        _ = shell.Press("G");
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        _ = shell.Press("Q");

        Assert.Equal(PageId.Queue, shell.CurrentPage);
    }
}
