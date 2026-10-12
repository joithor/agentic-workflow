using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Core.Engine;

/// <summary>
/// Reads and checks the profile's 'context:' section (spec §8.8). Every refusal is SND-CONTEXT-001 with the key path
/// first ('context.deadline: …'), so 'profile validate' can show it as written.
/// </summary>
public static partial class SettingsReader
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SettingsJsonContext.Default);

    /// <summary>The checked settings for <paramref name="section"/> (defaults for a missing or null section).</summary>
    /// <param name="section">The section.</param>
    /// <returns>The settings, or SND-CONTEXT-001.</returns>
    public static Result<ContextSettings> Read(JsonElement? section)
    {
        if (section is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } element)
        {
            return new ContextSettings();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid("context", "must be a map of settings");
        }

        if (UnknownKey(element) is { } unknown)
        {
            return Invalid("context." + unknown, "is not a context setting this build knows (repos, docs, deadline, maxBudget, minHits, sessions, sources)");
        }

        if (SourcesReader.Shape(element) is { } shape)
        {
            return shape;
        }

        var read = AwJson.Deserialize<ContextSection>(System.Text.Encoding.UTF8.GetBytes(element.GetRawText()), _json);
        return read.IsError ? Invalid("context", read.Error.Message) : Check(read.Value);
    }

    private static Result<ContextSettings> Check(ContextSection section)
    {
        var settings = new ContextSettings();
        var globs = section.Docs?.Globs ?? ContextSettings.DefaultDocGlobs;
        var deadline = section.Deadline is null ? settings.Deadline : Duration(section.Deadline);
        var sources = SourcesReader.Check(section.Sources);
        return (Problem(section, globs), deadline, sources) switch
        {
            ({ } problem, _, _) => problem,
            (_, null, _) => Invalid("context.deadline", "is a duration from 100ms to 60s, such as 4s or 500ms"),
            (_, _, { IsError: true }) => sources.Error,
            _ => settings with
            {
                Repos = section.Repos,
                DocGlobs = globs,
                Deadline = deadline.Value,
                MaxBudget = section.MaxBudget ?? settings.MaxBudget,
                MinHits = section.MinHits ?? settings.MinHits,
                SessionsEnabled = section.Sessions?.Enabled ?? false,
                Onyx = sources.Value.Onyx,
                LinearTeams = sources.Value.Teams,
            },
        };
    }

    /// <summary>The first refused key of <paramref name="section"/> other than the deadline, or null.</summary>
    private static Error? Problem(ContextSection section, IReadOnlyList<string> globs)
    {
        if (section.Repos is { } repos && repos.Any(string.IsNullOrWhiteSpace))
        {
            return Invalid("context.repos", "names profile repos; an entry is empty");
        }

        if (globs.Count == 0)
        {
            return Invalid("context.docs.globs", "needs at least one glob");
        }

        if (globs.FirstOrDefault(g => !GlobShape.IsMatch(g)) is { } badGlob)
        {
            return Invalid("context.docs.globs", $"'{Printable(badGlob)}' isn't a repo-relative glob (letters, digits, '._-/*', no '..', no leading '/')");
        }

        return section switch
        {
            { MaxBudget: < 1_000 or > 200_000 } => Invalid("context.maxBudget", "is 1000 to 200000 tokens"),
            { MinHits: < 0 or > 50 } => Invalid("context.minHits", "is 0 to 50"),
            _ => null,
        };
    }

    private static string? UnknownKey(JsonElement element)
    {
        string[] known = ["repos", "docs", "deadline", "maxBudget", "minHits", "sessions", "sources"];
        return element.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !known.Contains(n, StringComparer.Ordinal)) is { } name ? Printable(name) : null;
    }

    private static TimeSpan? Duration(string text)
    {
        var match = DurationShape.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = int.Parse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        var span = string.Equals(match.Groups["u"].Value, "ms", StringComparison.Ordinal) ? TimeSpan.FromMilliseconds(value) : TimeSpan.FromSeconds(value);
        return span >= TimeSpan.FromMilliseconds(100) && span <= TimeSpan.FromSeconds(60) ? span : null;
    }

    /// <summary>A name or value from the profile, for a message: itself when it is plain, else a fixed word.</summary>
    internal static string Printable(string text) => PlainText.IsMatch(text) ? text : "(a value with unusual characters)";

    /// <summary>SND-CONTEXT-001 with the key path first.</summary>
    internal static Error Invalid(string keyPath, string problem) => ContextErrors.SettingsInvalid.With($"{keyPath}: {problem}");

    [GeneratedRegex("^(?<n>[0-9]{1,6})(?<u>ms|s)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DurationShape { get; }

    [GeneratedRegex("^(?!/)(?!.*\\.\\.)[A-Za-z0-9._*/-]{1,200}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GlobShape { get; }

    [GeneratedRegex("^[A-Za-z0-9._*/ -]{1,80}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlainText { get; }
}
