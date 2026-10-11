namespace Aw.Sindri.Ui.Navigation;

/// <summary>
/// A place inside the app (UI spec §3.3): 'home/needs-you', 'needs-you/&lt;rowId&gt;', 'item/&lt;ItemId&gt;',
/// 'decision/&lt;DecisionId&gt;', 'service/&lt;name&gt;' or 'job/&lt;JobId&gt;'. The tray, the palette and a
/// second launch use them; a deep link only navigates. Subjects are opaque to the app: one segment of letters, digits and
/// '-_.:', at most 128 characters.
/// </summary>
/// <param name="Kind">What it opens.</param>
/// <param name="Subject">The row, item, decision, service or job; null for <see cref="DeepLinkKind.HomeNeedsYou"/>.</param>
internal sealed record DeepLink(DeepLinkKind Kind, string? Subject)
{
    /// <summary>Home with Needs you expanded.</summary>
    internal static DeepLink HomeNeedsYou { get; } = new(DeepLinkKind.HomeNeedsYou, Subject: null);

    /// <summary>Parses a deep link's text.</summary>
    /// <param name="text">The text (from a second launch, the tray or the palette).</param>
    /// <param name="link">The link, when the text is one.</param>
    /// <returns>True when <paramref name="text"/> is a deep link.</returns>
    internal static bool TryParse(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeepLink? link) => throw new NotSupportedException();

    /// <summary>The link's text, the form <see cref="TryParse"/> reads.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => throw new NotSupportedException();
}
