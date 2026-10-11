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
    internal static bool TryParse(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeepLink? link)
    {
        link = null;
        if (string.Equals(text, HomeNeedsYouText, StringComparison.Ordinal))
        {
            link = HomeNeedsYou;
            return true;
        }

        var slash = text?.IndexOf('/', StringComparison.Ordinal) ?? -1;
        if (slash < 0)
        {
            return false;
        }

        var kind = _kinds.FirstOrDefault(k => text.AsSpan(0, slash).SequenceEqual(k.Prefix)).Kind;
        var subject = text![(slash + 1)..]; // why: slash >= 0 only when text isn't null
        if (kind is null || !IsSubject(subject))
        {
            return false;
        }

        link = new DeepLink(kind.Value, subject);
        return true;
    }

    /// <summary>The link's text, the form <see cref="TryParse"/> reads.</summary>
    /// <returns>The text.</returns>
    public override string ToString() =>
        Kind == DeepLinkKind.HomeNeedsYou ? HomeNeedsYouText : _kinds.First(k => k.Kind == Kind).Prefix + "/" + Subject;

    private const string HomeNeedsYouText = "home/needs-you";

    private const int MaxSubject = 128;

    private static readonly (string Prefix, DeepLinkKind? Kind)[] _kinds =
    [
        ("needs-you", DeepLinkKind.NeedsYou),
        ("item", DeepLinkKind.Item),
        ("decision", DeepLinkKind.Decision),
        ("service", DeepLinkKind.Service),
        ("job", DeepLinkKind.Job),
    ];

    private static bool IsSubject(string s) =>
        s.Length is > 0 and <= MaxSubject && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
