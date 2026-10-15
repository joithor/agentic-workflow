namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

#pragma warning disable MA0041, CA1822, MA0182 // red stub (U3' T5)

/// <summary>The one polite live region's line when rows arrive (UI spec §5): "N now need you", once for each rise.</summary>
internal sealed class AttentionAnnouncer
{
    /// <summary>The line; empty before the first announcement.</summary>
    internal string Line => "";

    /// <summary>How many announcements were made.</summary>
    internal int Announcements => 0;

    /// <summary>Sees the count of rows that need the user now.</summary>
    /// <param name="count">The count.</param>
    internal void Observe(int count) => throw new NotSupportedException();
}
