using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.CodeIndex;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line of the card: the words, and the accessible name that leads with the card's name.</summary>
/// <param name="Text">"repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%".</param>
internal sealed record CodeIndexLine(string Text)
{
    /// <summary>The line read aloud: "Code index: " first.</summary>
    public string Name { get; } = Labels.CodeIndexNamePrefix + Text;
}

/// <summary>
/// Health's Code index card (code-index spec §7.3, CI-10b): one line per repo in scope from GET /sindri/code-index/status, the words keyed by the
/// index's state. Followed like the other views: read on connect and after every event, never folded.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="clock">For the age of an index.</param>
internal sealed partial class CodeIndexCardViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    private const int ShortSha = 7;

    // The layers every line names; any other layer (clones, deps) appears only when it needs a look.
    private static readonly string[] _named = ["structure", "graph", "embeddings"];

    /// <summary>The card's heading.</summary>
    public string Heading { get; } = Labels.CodeIndexCard;

    /// <summary>The lines, one per repo; empty when none is in scope or nothing has been read.</summary>
    public IReadOnlyList<CodeIndexLine> Lines { get; private set => Set(ref field, value); } = [];

    /// <summary>What shows instead of lines: the reading, none-in-scope or unreadable words; empty while lines show.</summary>
    public string Message { get; private set => Set(ref field, value); } = Labels.ReadingCodeIndex;

    /// <summary>True when there is a message to show.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The state in a word at the heading's right: "ok" or "needs a look"; empty with no lines.</summary>
    public string Word { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads the status and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var read = await api.CodeIndexStatus(url, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            Lines = [];
            Word = string.Empty;
            Show(Labels.CodeIndexUnreadable);
            return;
        }

        var repos = read.Value.Repos;
        Lines = [.. repos.Select(r => new CodeIndexLine(Line(r)))];
        Word = repos.Count == 0 ? string.Empty : repos.All(Fine) ? Labels.CodeIndexOk : Labels.CodeIndexNeedsLook;
        Show(repos.Count == 0 ? Labels.CodeIndexNone : string.Empty);
    }

    private void Show(string message)
    {
        Message = message;
        OnChanged(nameof(HasMessage));
    }

    private static bool Fine(CodeIndexRepoView repo) =>
        string.Equals(repo.State, "ok", StringComparison.Ordinal) && repo.Layers.All(static l => l.State is "ok" or "disabled" or "pending");

    private string Line(CodeIndexRepoView repo)
    {
        if (repo.Commit is not { Length: > 0 } commit)
        {
            return repo.Repo + ": " + Labels.CodeIndexState(repo.State);
        }

        var age = repo.BuiltAt is { } built ? ", " + Labels.CodeIndexAge(clock.GetUtcNow() - built) : string.Empty;
        var state = string.Equals(repo.State, "ok", StringComparison.Ordinal) ? string.Empty : " (" + Labels.CodeIndexState(repo.State) + ")";
        var layers = string.Join(" · ", repo.Layers.Where(Shown).Select(Words));
        return repo.Repo + " @ " + commit[..Math.Min(ShortSha, commit.Length)] + age + state + (layers.Length == 0 ? string.Empty : ", " + layers);
    }

    private static bool Shown(CodeIndexLayerView layer) => _named.Contains(layer.Layer, StringComparer.Ordinal) || layer.State is not ("ok" or "disabled");

    private static string Words(CodeIndexLayerView layer) => Labels.CodeIndexLayer(layer.Layer, layer.State, layer.Code, Percent(layer));

    // The embeddings layer says "31 of 100 embedded"; that is the only percentage the status view carries.
    [GeneratedRegex(@"^(\d+) of (\d+) embedded$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Embedded();

    private static int? Percent(CodeIndexLayerView layer)
    {
        if (!string.Equals(layer.State, "pending", StringComparison.Ordinal) || Embedded().Match(layer.Detail) is not { Success: true } m
            || !long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var done)
            || !long.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var all) || all <= 0)
        {
            return null;
        }

        return (int)Math.Min(100, done * 100 / all);
    }
}
