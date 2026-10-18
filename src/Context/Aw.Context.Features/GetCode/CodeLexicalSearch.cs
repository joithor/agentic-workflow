using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Features.GetCode;

/// <summary>
/// The lexical half of the code lens (S-CL): the test files that name an implementing file and the schema files that name a data thing, found by reading the repo at one commit through git (never the
/// working tree) within the profile's deadline. File level: a path and the form that matched leave this class, never a line or text. A repo that can't be read, a commit the clone lacks or an expired
/// deadline is a row of 'searched', never an error of the route.
/// </summary>
internal sealed class CodeLexicalSearch(IRepoReader repos, LexicalTestsRule tests, LexicalSchemaRule schema, TimeProvider clock)
{
    internal async ValueTask<CodeLexicalFindings> Find(CodeLexicalRequest request, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(request.Settings.Deadline);
        var pass = new LexicalPass(request.Settings, request.Map.Graph, request.View.Data.Things, request.Clean);
        try
        {
            await Run(pass, request, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            pass.TimedOut = true;
        }

        return pass.Findings(clock.GetUtcNow());
    }

    private async ValueTask Run(LexicalPass pass, CodeLexicalRequest request, CancellationToken cancellationToken)
    {
        var inScope = ScopeRules.Resolve(request.Settings, request.Scope.Repos, request.Scope.Caller);
        var allowed = inScope.IsError ? [] : inScope.Value;
        var implementing = Implementing(request.View, out var capped);
        pass.Capped = capped;
        var byRepo = Group(pass, request.Map.Graph, implementing, allowed);
        var version = request.Map.Snapshot.Version;
        var targets = allowed.Where(r => byRepo.ContainsKey(r.Name.Value)).ToList();
        if (targets.Count == 0 && allowed.FirstOrDefault(r => r.Name == version.Repo) is { } mapRepo)
        {
            targets.Add(mapRepo);
        }

        foreach (var repo in targets)
        {
            var mapCommit = repo.Name == version.Repo && IsSha(version.Commit) ? version.Commit : null;
            await Repo(pass, repo, mapCommit, byRepo.GetValueOrDefault(repo.Name.Value) ?? [], cancellationToken).ConfigureAwait(false);
        }
    }

    // The implementing files by the repo their evidence names; a file no allowed repo claims is counted as unlocated.
    private static Dictionary<string, List<string>> Group(LexicalPass pass, GraphSnapshot graph, List<string> implementing, IReadOnlyList<ContextRepo> allowed)
    {
        var byRepo = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in implementing)
        {
            if (RepoOf(graph, path, allowed) is not { } repo)
            {
                pass.Unlocated++;
                continue;
            }

            if (!byRepo.TryGetValue(repo.Name.Value, out var list))
            {
                byRepo[repo.Name.Value] = list = [];
            }

            list.Add(path);
        }

        return byRepo;
    }

    // The files whose tests are searched: confirmed first, then by confidence, then path, at most 50.
    private static List<string> Implementing(CodeView view, out bool capped)
    {
        var ordered = view.Files.OrderByDescending(f => f.Confirmed).ThenByDescending(f => f.Confidence).ThenBy(f => f.Path, StringComparer.Ordinal).ToList();
        capped = ordered.Count > CodeRules.MaxImplementingFiles || view.Truncated.Files;
        return [.. ordered.Take(CodeRules.MaxImplementingFiles).Select(f => f.Path)];
    }

    // The repo a file belongs to: the one its implements edge's evidence names ('repo:<name>/<path>'), when the caller may read it.
    private static ContextRepo? RepoOf(GraphSnapshot graph, string path, IReadOnlyList<ContextRepo> allowed)
    {
        var key = "code:" + path;
        return graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Value == key)
            .SelectMany(e => e.EvidenceOrEmpty)
            .Select(r => allowed.FirstOrDefault(a => r.Key.Value.StartsWith($"repo:{a.Name.Value}/", StringComparison.Ordinal)))
            .FirstOrDefault(r => r is not null);
    }

    private async ValueTask Repo(LexicalPass pass, ContextRepo repo, string? mapCommit, List<string> files, CancellationToken cancellationToken)
    {
        var commit = mapCommit ?? (await repos.Head(repo, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } head ? head.Value : null);
        if (commit is null)
        {
            pass.Fail(ContextErrors.ExtractionUnreadable.Code, commitNotHeld: false, source: null);
            return;
        }

        var listed = await repos.ListFiles(repo, commit, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            // A commit the map named that the clone can't list is a commit it doesn't hold; HEAD failing to list is a checkout git can't read.
            pass.Fail(mapCommit is null ? listed.Error.Code : ContextErrors.CommitUnknown.Code, commitNotHeld: mapCommit is not null, source: null);
            return;
        }

        var run = new LexicalRun(pass, repo, commit, [.. listed.Value.Where(p => !repo.Denies(p))]);
        if (files.Count > 0)
        {
            pass.TestsStarted = true;
            await tests.Find(run, files, cancellationToken).ConfigureAwait(false);
        }

        pass.SchemaStarted = true;
        await schema.Find(run, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSha(string? commit) => commit is { Length: >= 7 and <= 64 } && commit.All(char.IsAsciiHexDigitLower);
}
