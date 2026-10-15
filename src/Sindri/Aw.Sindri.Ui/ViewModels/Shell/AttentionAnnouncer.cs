namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The one polite live region's line when rows arrive (UI spec §5): "N now need you", once for each rise in the count. A
/// row leaving is announced by the list ("&lt;id&gt; cleared"), so a fall says nothing here.
/// </summary>
internal sealed class AttentionAnnouncer
{
    private int _last;

    /// <summary>The line; empty before the first announcement.</summary>
    internal string Line { get; private set; } = "";

    /// <summary>How many announcements were made.</summary>
    internal int Announcements { get; private set; }

    /// <summary>Sees the count of rows that need the user now.</summary>
    /// <param name="count">The count.</param>
    internal void Observe(int count)
    {
        if (count > _last)
        {
            Line = Labels.NowNeedYou(count);
            Announcements++;
        }

        _last = count;
    }
}
