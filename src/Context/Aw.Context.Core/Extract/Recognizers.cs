using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>The forms the extractor recognizes (S-4), each pure over one file's text. Only source files count: not tests, not vendored or built output.</summary>
public static partial class Recognizers
{
    private const int MaxFileLength = 512_000;
    private static readonly string[] _extensions = [".ts", ".tsx", ".js", ".jsx", ".mjs", ".json"];
    private static readonly string[] _skipped = ["node_modules/", "dist/", "build/", "coverage/", ".git/", "tests/", "__tests__/"];

    /// <summary>Every recognizer, in the order their findings are reported.</summary>
    public static IReadOnlyList<Recognizer> All { get; } =
    [
        new("command-ids", Source, f => CommandIds(f)),
        new("command-registry", Source, f => Registry(f)),
        new("keybindings", Source, f => Keybindings(f)),
        new("settings", f => Source(f) && Settings().IsMatch(f.Path), f => SettingKeys(f)),
        new("routes", Source, f => Routes(f)),
        new("components", f => Source(f) && f.Path.EndsWith("x", StringComparison.Ordinal) && f.Path.Contains("sx", StringComparison.Ordinal), f => Components(f)),
        new("test-ids", Source, f => TestIds(f)),
    ];

    /// <summary>Whether the extractor reads a file at all.</summary>
    /// <param name="file">The file.</param>
    /// <returns>True for a small source file outside tests and build output.</returns>
    public static bool Source(RepoFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = "/" + file.Path;
        return file.Text.Length <= MaxFileLength
            && _extensions.Any(e => file.Path.EndsWith(e, StringComparison.Ordinal))
            && !_skipped.Any(s => path.Contains("/" + s, StringComparison.Ordinal))
            && !file.Path.Contains(".test.", StringComparison.Ordinal) && !file.Path.Contains(".spec.", StringComparison.Ordinal);
    }

    private static IEnumerable<Found> CommandIds(RepoFile file) =>
        CommandUnion().Matches(file.Text).SelectMany(m => Literal().Matches(m.Groups["body"].Value))
            .Select(m => Command(m.Groups["v"].Value, Humanize(m.Groups["v"].Value), named: false));

    private static IEnumerable<Found> Registry(RepoFile file) =>
        RegistryEntry().Matches(file.Text).Select(m => Command(m.Groups["id"].Value, m.Groups["title"].Value, named: true));

    private static IEnumerable<Found> Keybindings(RepoFile file) =>
        KeyTable().Matches(file.Text).SelectMany(t => Binding().Matches(t.Groups["body"].Value)).SelectMany(m =>
        {
            var combo = Make("surface:keybinding/" + m.Groups["combo"].Value);
            var command = Make("surface:command/" + m.Groups["id"].Value);
            return combo is null ? [] : new[] { new Found(combo.Value, m.Groups["combo"].Value, 1.0, true, command is null ? [] : [new GraphEdge(combo.Value, command.Value, EdgeType.MovesTo, Results.Provenance.Deterministic)]) };
        });

    private static IEnumerable<Found> SettingKeys(RepoFile file)
    {
        System.Text.Json.JsonDocument document;
        try
        {
            document = System.Text.Json.JsonDocument.Parse(file.Text);
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }

        using (document)
        {
            return document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ? [] :
            [.. document.RootElement.EnumerateObject().Select(p => (Name: p.Name, Key: Make("surface:setting/" + p.Name)))
                .Where(p => p.Key is not null).Select(p => new Found(p.Key!.Value, p.Name, 1.0, true, []))];   // why: filtered to non-null keys above
        }
    }

    private static IEnumerable<Found> Routes(RepoFile file) =>
        Route().Matches(file.Text).Select(m => (Method: m.Groups["m"].Value.ToUpperInvariant(), Path: m.Groups["p"].Value))
            .Select(r => (r.Method, r.Path, Key: Make($"surface:route/{r.Method}:{r.Path}")))
            .Where(r => r.Key is not null).Select(r => new Found(r.Key!.Value, $"{r.Method} {r.Path}", 1.0, true, []));   // why: filtered to non-null keys above

    private static IEnumerable<Found> Components(RepoFile file) =>
        Component().Matches(file.Text).Select(m => m.Groups["n"].Value).Select(n => (Name: n, Key: Make($"surface:{ComponentKind(n)}/{n}")))
            .Where(c => c.Key is not null).Select(c => new Found(c.Key!.Value, c.Name, 0.9, true, []));   // why: filtered to non-null keys above

    private static IEnumerable<Found> TestIds(RepoFile file) =>
        TestId().Matches(file.Text).Select(m => m.Groups["id"].Value).Select(id => (Id: id, Key: Make("surface:pane/" + id)))
            .Where(c => c.Key is not null).Select(c => new Found(c.Key!.Value, c.Id, 0.8, true, []));   // why: filtered to non-null keys above

    private static string ComponentKind(string name) =>
        name.EndsWith("Dialog", StringComparison.Ordinal) || name.EndsWith("Modal", StringComparison.Ordinal) ? "dialog"
        : name.EndsWith("Screen", StringComparison.Ordinal) || name.EndsWith("Page", StringComparison.Ordinal) ? "screen"
        : "pane";

    private static Found Command(string id, string label, bool named)
    {
        var key = Make("surface:command/" + id) ?? NodeKey.From("surface:command/unnamed");
        return new Found(key, label, 1.0, named, []);
    }

    private static NodeKey? Make(string value) => NodeKey.TryFrom(value, out var key) ? key : null;

    private static string Humanize(string id)
    {
        var words = id.Replace('-', ' ').Replace('_', ' ');
        return words.Length == 0 ? id : string.Concat(words[..1].ToUpper(CultureInfo.InvariantCulture), words.AsSpan(1));
    }

    [GeneratedRegex("(?:^|/)[^/]*settings[^/]*\\.json\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Settings();

    [GeneratedRegex("type\\s+\\w*Command(?:Id|Name)\\s*=\\s*(?<body>(?:\\s*\\|?\\s*\"[^\"\\r\\n]+\")+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommandUnion();

    [GeneratedRegex("\"(?<v>[^\"\\r\\n]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Literal();

    [GeneratedRegex("\\bid:\\s*\"(?<id>[A-Za-z0-9][\\w.-]*)\"\\s*,\\s*title:\\s*\"(?<title>[^\"\\r\\n]*)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RegistryEntry();

    [GeneratedRegex("Record<\\s*string\\s*,\\s*\\w*Command(?:Id|Name)\\s*>\\s*=\\s*\\{(?<body>[^}]*)\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyTable();

    [GeneratedRegex("\"(?<combo>[^\"\\r\\n]+)\"\\s*:\\s*\"(?<id>[^\"\\r\\n]+)\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Binding();

    [GeneratedRegex("\\b(?:app|router)\\.(?<m>get|post|put|patch|delete)\\(\\s*[\"'`](?<p>/[^\"'`\\s]*)[\"'`]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Route();

    [GeneratedRegex("\\bexport\\s+(?:default\\s+)?(?:function|const|class)\\s+(?<n>[A-Z][A-Za-z0-9]{2,})", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Component();

    [GeneratedRegex("data-testid=[\"'](?<id>[A-Za-z0-9][\\w.-]*)[\"']", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestId();
}
