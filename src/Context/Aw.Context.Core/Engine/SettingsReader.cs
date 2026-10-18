using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Context.Core.Trust;
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

    /// <summary>
    /// The section checked as <see cref="Read"/> does, then against the profile's repos: with a section, 'context.repos'
    /// names only profile repos and every repo in scope needs a usable name; without one, nothing about the repos is
    /// refused. The one place a section counts as present (not null, not undefined).
    /// </summary>
    /// <param name="section">The section, or null.</param>
    /// <param name="repos">The profile's repos as written.</param>
    /// <returns>The settings, or SND-CONTEXT-001 naming the key path.</returns>
    public static Result<ContextSettings> Check(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) =>
        Read(section).Bind(settings => ScopeRules.CheckRepos(section is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }, settings, repos));

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
            return Invalid("context." + unknown, "is not a context setting this build knows (repos, docs, cascadeDepth, cascadeComponentFanOut, deadline, maxBudget, minHits, labeling, scrub, sessions, sources, eval, extract)");
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
        return (Refused(section, globs), deadline, sources) switch
        {
            ({ } problem, _, _) => problem,
            (_, null, _) => Invalid("context.deadline", "is a duration from 100ms to 60s, such as 4s or 500ms"),
            (_, _, { IsError: true }) => sources.Error,
            _ => WithFanOut(settings, section) with
            {
                Repos = section.Repos?.Select(ContextRepoName.From).ToList(),
                DocGlobs = globs,
                Deadline = deadline.Value,
                MaxBudget = section.MaxBudget ?? settings.MaxBudget,
                MinHits = section.MinHits ?? settings.MinHits,
                CascadeDepth = section.CascadeDepth ?? settings.CascadeDepth,
                SessionsEnabled = section.Sessions?.Enabled ?? false,
                Onyx = sources.Value.Onyx,
                LinearTeams = sources.Value.Teams,
                ExtraPatterns = Patterns(section.Scrub),
                Labeling = Labeling(section.Labeling),
                Adjudicator = Adjudicates(section.Eval),
                Recognizers = Recognizers(section.Extract),
            },
        };
    }

    /// <summary>The recognizers as written (a null entry was refused by <see cref="RecognizerRules"/>).</summary>
    private static IReadOnlyList<ProfileRecognizer> Recognizers(ExtractSection? extract) =>
        [.. (extract?.Recognizers ?? []).OfType<RecognizerEntry>().Select(e => new ProfileRecognizer(e.Kind!, e.Glob!, e.Pattern!, e.KeyFrom!))];   // why: RecognizerRules refused a missing field

    private static Error? Refused(ContextSection section, IReadOnlyList<string> globs) => Problem(section, globs) ?? RecognizerRules.Problem(section.Extract?.Recognizers) ?? GraphifyRules.Problem(section.Extract?.Graphify);

    private static bool Adjudicates(EvalSection? section) => section?.Adjudicator ?? false;

    private static LabelingSettings Labeling(LabelingSection? section) =>
        new()
        {
            Enabled = section?.Enabled ?? false,
            MaxTokens = section?.MaxTokens ?? new LabelingSettings().MaxTokens,
            MaxTokensPerSync = section?.MaxTokensPerSync ?? new LabelingSettings().MaxTokensPerSync,
        };

    /// <summary>The extra patterns as written (a null entry was refused by <see cref="Problem"/>).</summary>
    private static IReadOnlyList<ScrubPattern> Patterns(ScrubSection? scrub) =>
        [.. (scrub?.ExtraPatterns ?? []).OfType<ScrubPatternEntry>().Select(e => new ScrubPattern(e.Id, e.Pattern))];

    /// <summary>The first refused key of <paramref name="section"/> other than the deadline, or null.</summary>
    private static Error? Problem(ContextSection section, IReadOnlyList<string> globs)
    {
        if (RepoProblem(section.Repos) is { } repoProblem)
        {
            return repoProblem;
        }

        if (globs.Count == 0)
        {
            return Invalid("context.docs.globs", "needs at least one glob");
        }

        if (globs.Any(g => g is null || !GlobShape.IsMatch(g)))
        {
            var badGlob = globs.First(g => g is null || !GlobShape.IsMatch(g));
            return Invalid("context.docs.globs", $"'{(badGlob is null ? "(empty)" : Printable(badGlob))}' isn't a repo-relative glob (letters, digits, '._-/*', no '..', no leading '/')");
        }

        var empty = (section.Scrub?.ExtraPatterns ?? []).ToList().FindIndex(e => e is null);   // the entries themselves are checked by IScrubberFactory
        if (empty >= 0)
        {
            return Invalid(string.Create(CultureInfo.InvariantCulture, $"context.scrub.extraPatterns[{empty}]"), "is empty; write { id: <rule id>, pattern: <regex> }");
        }

        return section switch
        {
            { MaxBudget: < 1_000 or > 200_000 } => Invalid("context.maxBudget", "is 1000 to 200000 tokens"),
            { MinHits: < 0 or > 50 } => Invalid("context.minHits", "is 0 to 50"),
            { Labeling.MaxTokens: < 1_000 or > 20_000_000 } => Invalid("context.labeling.maxTokens", "is 1000 to 20000000 tokens"),
            { Labeling.MaxTokensPerSync: < 1 or > 20_000_000 } => Invalid("context.labeling.maxTokensPerSync", "is 1 to 20000000 tokens"),
            _ => CascadeProblem(section),
        };
    }

    private static ContextSettings WithFanOut(ContextSettings settings, ContextSection section) =>
        settings with { CascadeComponentFanOut = section.CascadeComponentFanOut ?? settings.CascadeComponentFanOut, Graphify = GraphifyRules.Limits(section.Extract?.Graphify) };

    private static Error? CascadeProblem(ContextSection section) => section switch
    {
        { CascadeDepth: < 0 or > QueryRules.MaxDepth } => Invalid("context.cascadeDepth", "is 0 to 4 steps"),
        { CascadeComponentFanOut: < 1 or > 50 } => Invalid("context.cascadeComponentFanOut", "is 1 to 50 features"),
        _ => null,
    };

    private static Error? RepoProblem(IReadOnlyList<string>? repos)
    {
        var bad = repos?.FirstOrDefault(r => r is null || !ContextRepoName.TryFrom(r, out _));
        return repos is not null && repos.Any(r => r is null || !ContextRepoName.TryFrom(r, out _))
            ? Invalid("context.repos", $"'{(bad is null ? "(empty)" : Printable(bad))}' isn't a repo name: 1 to 100 letters, digits, '.', '_' or '-'")
            : null;
    }

    private static string? UnknownKey(JsonElement element)
    {
        string[] known = ["repos", "docs", "cascadeDepth", "cascadeComponentFanOut", "deadline", "maxBudget", "minHits", "labeling", "scrub", "sessions", "sources", "eval", "extract"];
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

    /// <summary>Whether text is a repo-relative glob the profile may name.</summary>
    internal static bool IsGlob(string text) => GlobShape.IsMatch(text);

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
