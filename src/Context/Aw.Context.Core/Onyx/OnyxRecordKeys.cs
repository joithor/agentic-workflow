using System.Text.RegularExpressions;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Onyx;

/// <summary>
/// The canonical key, kind and alias of an Onyx document (spec §5.1, D16), from what Onyx CE v4.9.0's connectors write:
/// <list type="bullet">
/// <item>Linear (connectors/linear/connector.py): the document id is the issue's UUID, so the key is
/// 'linear:issue/&lt;uuid&gt;' (lower case). The identifier ('ENG-482') becomes an alias only when the connector-built
/// title prefix '[ENG-482] ' and the link 'https://linear.app/&lt;workspace&gt;/issue/ENG-482[/…]' name the same identifier,
/// it matches '^[A-Z][A-Z0-9]+-[0-9]+$', and its team key is one of the profile's teams. A Linear id that isn't a UUID
/// keys as an Onyx record with no alias.</item>
/// <item>Slack (connectors/slack/connector.py _build_doc_id): '&lt;channel&gt;__&lt;thread_ts&gt;' keys as
/// 'slack:&lt;channel&gt;/&lt;thread_ts&gt;'.</item>
/// <item>Anything else: 'onyx:&lt;source_type&gt;/&lt;document id&gt;'.</item>
/// </list>
/// Body text never contributes: only the connector-built title, link and id.
/// </summary>
public static partial class OnyxRecordKeys
{
    private const string LinearSource = "linear";
    private const string SlackSource = "slack";
    private const string LinearIssuePrefix = "linear:issue/";
    private const string SlackPrefix = "slack:";
    private const string OnyxPrefix = "onyx:";

    /// <summary>A Linear team key as the identifier alias reads it: a capital letter, then one or more capitals or digits ('ENG').</summary>
    internal const string TeamKeyPattern = "[A-Z][A-Z0-9]+";

    /// <summary>Identifies one Onyx document.</summary>
    /// <param name="document">Its Onyx document id.</param>
    /// <param name="sourceType">Its connector ('linear', 'slack', …).</param>
    /// <param name="semanticIdentifier">Its title as the connector built it.</param>
    /// <param name="link">Its link, if any.</param>
    /// <param name="teams">The Linear team keys an alias may name.</param>
    /// <returns>The identity, or null when the id can't form a key (too long once prefixed).</returns>
    public static OnyxRecordIdentity? Identify(ExternalId document, string sourceType, string semanticIdentifier, string? link, IReadOnlySet<string> teams)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(semanticIdentifier);
        ArgumentNullException.ThrowIfNull(teams);
        var id = document.Value;
        if (string.Equals(sourceType, LinearSource, StringComparison.Ordinal) && Guid.TryParseExact(id, "D", out var issue))
        {
            var key = RecordKey.From(LinearIssuePrefix + issue.ToString("D"));
            return new OnyxRecordIdentity(key, RecordKind.Issue, Alias(semanticIdentifier, link, teams));
        }

        if (string.Equals(sourceType, SlackSource, StringComparison.Ordinal) && SlackThread.Match(id) is { Success: true } thread)
        {
            return Keyed($"{SlackPrefix}{thread.Groups["channel"].Value}/{thread.Groups["ts"].Value}", RecordKind.Message);
        }

        return Keyed($"{OnyxPrefix}{sourceType}/{id}", KindOfSource(sourceType));
    }

    /// <summary>The kind of the record a canonical key from <see cref="Identify"/> names.</summary>
    /// <param name="canonical">The key.</param>
    /// <returns>Issue for a Linear issue, Message for a Slack thread, otherwise Doc.</returns>
    public static RecordKind KindOf(RecordKey canonical)
    {
        var key = canonical.Value;
        if (key.StartsWith(LinearIssuePrefix, StringComparison.Ordinal))
        {
            return RecordKind.Issue;
        }

        if (key.StartsWith(SlackPrefix, StringComparison.Ordinal))
        {
            return RecordKind.Message;
        }

        var source = key.StartsWith(OnyxPrefix, StringComparison.Ordinal) ? key[OnyxPrefix.Length..].Split('/')[0] : "";
        return KindOfSource(source);
    }

    private static RecordKind KindOfSource(string sourceType) => sourceType switch
    {
        LinearSource => RecordKind.Issue,
        SlackSource => RecordKind.Message,
        _ => RecordKind.Doc,
    };

    private static OnyxRecordIdentity? Keyed(string key, RecordKind kind) =>
        RecordKey.TryFrom(key, out var canonical) ? new OnyxRecordIdentity(canonical, kind, Alias: null) : null;

    /// <summary>The identifier the title prefix and the issue link agree on, when it is a profile team's; else null.</summary>
    private static string? Alias(string semanticIdentifier, string? link, IReadOnlySet<string> teams)
    {
        var prefix = TitlePrefix.Match(semanticIdentifier);
        if (!prefix.Success || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "linear.app", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort)
        {
            return null;
        }

        var identifier = prefix.Groups["id"].Value;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var linked = segments.Length >= 3 && string.Equals(segments[1], "issue", StringComparison.Ordinal) ? segments[2] : null;
        return string.Equals(identifier, linked, StringComparison.Ordinal) && teams.Contains(identifier[..identifier.IndexOf('-', StringComparison.Ordinal)])
            ? identifier
            : null;
    }

    [GeneratedRegex(@"^\[(?<id>" + TeamKeyPattern + @"-[0-9]+)\] ", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TitlePrefix { get; }

    [GeneratedRegex(@"^(?<channel>[A-Z][A-Z0-9]+)__(?<ts>[0-9]+\.[0-9]+)\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SlackThread { get; }
}
