using System.Globalization;
using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Publishing;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>
/// A scripted IGhRunner that records every call. By default gh answers exit 0 with no output; <see cref="Reply"/> scripts it, and
/// <see cref="WithGitData"/> turns it into an in-memory repository answering the git data API calls the evidence branch is written with
/// (gh api [--method M] repos/&lt;repo&gt;/git/{ref,commits,blobs,trees,refs}… with the JSON body on stdin, as the TypeScript verb called them).
/// </summary>
public sealed class FakeGhRunner : IGhRunner
{
    private const string NotFound = "gh: Not Found (HTTP 404)";

    private readonly List<GhCall> _calls = [];
    private readonly Dictionary<string, string> _branches = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _trees = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Tree, string[] Parents, string Message)> _commits = new(StringComparer.Ordinal);
    private Func<GhCall, GhOutcome>? _behavior;
    private string? _repo;
    private int _shas;

    /// <summary>Every call, in order.</summary>
    public IReadOnlyList<GhCall> Calls => _calls;

    /// <summary>Makes gh fail to start.</summary>
    public bool WontStart { get; set; }

    /// <summary>Makes every git data call fail with this stderr and exit 1 (gh exits non-zero on an HTTP error); null lets them through.</summary>
    public string? FailWith { get; set; }

    /// <summary>Scripts gh: every call is answered by <paramref name="behavior"/>.</summary>
    /// <param name="behavior">What gh does.</param>
    /// <returns>This fake.</returns>
    public FakeGhRunner Reply(Func<GhCall, GhOutcome> behavior)
    {
        _behavior = behavior;
        return this;
    }

    /// <summary>Turns this fake into an in-memory repository.</summary>
    /// <param name="repo">The repository, owner/name; a call for another repository gets a 404.</param>
    /// <returns>This fake.</returns>
    public FakeGhRunner WithGitData(string repo)
    {
        _repo = repo;
        return this;
    }

    /// <summary>The commit a branch points at, or null when it does not exist.</summary>
    /// <param name="branch">The branch, e.g. ui-evidence/pr-7.</param>
    /// <returns>The commit sha, or null.</returns>
    public string? Head(string branch) => _branches.GetValueOrDefault(branch);

    /// <summary>Creates a branch holding the given files in one commit (an earlier run's evidence).</summary>
    /// <param name="branch">The branch.</param>
    /// <param name="files">Repository path to content.</param>
    /// <returns>The commit sha.</returns>
    public string Seed(string branch, IReadOnlyDictionary<string, byte[]> files)
    {
        var tree = files.ToDictionary(static f => f.Key, f => NewBlob(f.Value), StringComparer.Ordinal);
        var treeSha = NewSha();
        _trees[treeSha] = tree;
        var commit = NewSha();
        _commits[commit] = (treeSha, [], "seed");
        _branches[branch] = commit;
        return commit;
    }

    /// <summary>The files in a commit, by repository path.</summary>
    /// <param name="commit">The commit sha.</param>
    /// <returns>Path to content.</returns>
    public IReadOnlyDictionary<string, byte[]> FilesAt(string commit) =>
        _trees[_commits[commit].Tree].ToDictionary(static f => f.Key, f => (byte[])_blobs[f.Value].Clone(), StringComparer.Ordinal);

    /// <summary>A commit's parents.</summary>
    /// <param name="commit">The commit sha.</param>
    /// <returns>The parent shas, none for a root commit.</returns>
    public IReadOnlyList<string> ParentsOf(string commit) => _commits[commit].Parents;

    /// <summary>A commit's message.</summary>
    /// <param name="commit">The commit sha.</param>
    /// <returns>The message.</returns>
    public string MessageOf(string commit) => _commits[commit].Message;

    /// <inheritdoc/>
    public ValueTask<Result<GhOutcome>> RunAsync(GhCall call, CancellationToken cancellationToken)
    {
        _calls.Add(call);
        if (WontStart)
        {
            return ValueTask.FromResult<Result<GhOutcome>>(EvidenceErrors.RunFailed.With("gh could not start"));
        }

        var outcome = _behavior is { } behavior ? behavior(call)
            : _repo is not null ? (FailWith is { } stderr ? new GhOutcome(1, string.Empty, stderr) : GitData(call))
            : new GhOutcome(0, string.Empty, string.Empty);
        return ValueTask.FromResult<Result<GhOutcome>>(outcome);
    }

    private static GhOutcome Ok(object body) => new(0, JsonSerializer.Serialize(body), string.Empty);

    private static GhOutcome Missing() => new(1, string.Empty, NotFound);

    private GhOutcome GitData(GhCall call)
    {
        var args = call.Args;
        if (args.Count < 2 || args[0] != "api")
        {
            return new GhOutcome(1, string.Empty, "gh: unsupported call");
        }

        var explicitMethod = args.Count > 3 && args[1] == "--method";
        var method = explicitMethod ? args[2] : "GET";
        var path = explicitMethod ? args[3] : args[1];
        var prefix = $"repos/{_repo}/git/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Missing();
        }

        var rest = path[prefix.Length..];
        var body = call.Stdin is { } text ? JsonDocument.Parse(text).RootElement : default;
        return (method, rest) switch
        {
            ("GET", _) when rest.StartsWith("ref/heads/", StringComparison.Ordinal) => Ref(rest["ref/heads/".Length..]),
            ("GET", _) when rest.StartsWith("commits/", StringComparison.Ordinal) => Commit(rest["commits/".Length..]),
            ("POST", "blobs") => Ok(new { sha = NewBlob(Convert.FromBase64String(body.GetProperty("content").GetString()!)) }),
            ("POST", "trees") => NewTree(body),
            ("POST", "commits") => NewCommit(body),
            ("POST", "refs") => CreateRef(body),
            ("PATCH", _) when rest.StartsWith("refs/heads/", StringComparison.Ordinal) => MoveRef(rest["refs/heads/".Length..], body),
            _ => Missing(),
        };
    }

    private GhOutcome Ref(string branch) =>
        _branches.TryGetValue(branch, out var sha) ? Ok(new { @ref = $"refs/heads/{branch}", @object = new { sha, type = "commit" } }) : Missing();

    private GhOutcome Commit(string sha) =>
        _commits.TryGetValue(sha, out var commit) ? Ok(new { sha, tree = new { sha = commit.Tree } }) : Missing();

    private GhOutcome NewTree(JsonElement body)
    {
        var entries = body.TryGetProperty("base_tree", out var baseTree) && _trees.TryGetValue(baseTree.GetString()!, out var existing)
            ? new Dictionary<string, string>(existing, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in body.GetProperty("tree").EnumerateArray())
        {
            entries[entry.GetProperty("path").GetString()!] = entry.GetProperty("sha").GetString()!;
        }

        var sha = NewSha();
        _trees[sha] = entries;
        return Ok(new { sha });
    }

    private GhOutcome NewCommit(JsonElement body)
    {
        var sha = NewSha();
        string[] parents = [.. body.GetProperty("parents").EnumerateArray().Select(static p => p.GetString()!)];
        _commits[sha] = (body.GetProperty("tree").GetString()!, parents, body.GetProperty("message").GetString()!);
        return Ok(new { sha });
    }

    private GhOutcome CreateRef(JsonElement body)
    {
        var branch = body.GetProperty("ref").GetString()!["refs/heads/".Length..];
        if (_branches.ContainsKey(branch))
        {
            return new GhOutcome(1, string.Empty, "gh: Reference already exists (HTTP 422)");
        }

        _branches[branch] = body.GetProperty("sha").GetString()!;
        return Ok(new { });
    }

    private GhOutcome MoveRef(string branch, JsonElement body)
    {
        if (!_branches.TryGetValue(branch, out var head))
        {
            return Missing();
        }

        var sha = body.GetProperty("sha").GetString()!;
        var forced = body.TryGetProperty("force", out var force) && force.GetBoolean();
        if (!forced && !_commits[sha].Parents.Contains(head))
        {
            return new GhOutcome(1, string.Empty, "gh: Update is not a fast forward (HTTP 422)");
        }

        _branches[branch] = sha;
        return Ok(new { });
    }

    private string NewBlob(byte[] content)
    {
        var sha = NewSha();
        _blobs[sha] = content;
        return sha;
    }

    private string NewSha() => (++_shas).ToString("x40", CultureInfo.InvariantCulture);
}
