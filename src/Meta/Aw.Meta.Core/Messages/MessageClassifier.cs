using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Messages;

/// <summary>Classifies an inbound message body.</summary>
public static partial class MessageClassifier
{
    private const string OwnerQuestionPrefix = "OWNER QUESTION";

    /// <summary>Classifies <paramref name="body"/>: an owner question, a ready notice, a lane announcement or a request.</summary>
    /// <param name="body">The message text.</param>
    /// <returns>The classification.</returns>
    public static Classification Classify(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var text = body.Trim();
        if (text.StartsWith(OwnerQuestionPrefix, StringComparison.Ordinal))
        {
            return new Classification(MessageKind.OwnerQuestion);
        }

        if (Matches(ReadyPattern, text) is { } ready
            && int.TryParse(ready.Groups["pr"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pr))
        {
            return new Classification(MessageKind.Ready, Pr: pr);
        }

        return Matches(AnnouncePattern, text) is { } announce
            ? new Classification(MessageKind.LaneAnnounce, Lane: announce.Groups["lane"].Value.ToLowerInvariant())
            : new Classification(MessageKind.Request);
    }

    private static Match? Matches(Regex pattern, string text)
    {
        try
        {
            var match = pattern.Match(text);
            return match.Success ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null; // a timeout never classifies (spec §4c)
        }
    }

    [GeneratedRegex(@"\bready #(?<pr>[0-9]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex ReadyPattern { get; }

    [GeneratedRegex(@"^\s*(?!(?:the|a|an|this|that|my|your|our|each|every|which|one|same|new|old|whole)\s)(?<lane>[a-z][a-z-]*)\s+lane\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex AnnouncePattern { get; }
}
