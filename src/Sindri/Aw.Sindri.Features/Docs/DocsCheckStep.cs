using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Docs;

/// <summary>
/// 'step.docs.check' on the block runtime (docs-sync spec §7.2, API17): the docs commit holds doc paths only (DOCS-PATH-001: the profile's globs, or a doc the design
/// names), its relative links resolve (DOCS-LINK-001) and its markdown parses (DOCS-MD-001: every code fence closed). A worktree with no change fails
/// (DOCS-CHANGE-001), because there would be no docs PR to open. 'fact:docsScope' is { base, paths (the globs), candidates: [{ path, designNamed }] }.
/// </summary>
/// <param name="diff">Lists the paths the docs worktree changed since the scope's base.</param>
/// <param name="files">Reads the changed docs and the files their links name.</param>
internal sealed class DocsCheckStep(IRunDiff diff, IBriefFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.docs.check");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var workspace = StepFacts.Text(context, "fact:workspace");
        if (workspace is null || StepFacts.Read(context, "fact:docsScope") is not JsonObject scope || (string?)scope["base"] is not { } baseCommit)
        {
            return BlockStepResult.Failed("step.docs.check needs fact:docsScope (with its base) and fact:workspace (a path)");
        }

        var changed = await diff.ChangedPaths(workspace, baseCommit, cancellationToken).ConfigureAwait(false);
        if (changed.IsError)
        {
            return BlockStepResult.Failed($"step.docs.check can't read {workspace}: {changed.Error.Message}");
        }

        var findings = changed.Value.Count == 0
            ? [DocsFinding.Of("DOCS-CHANGE-001", item: null, "the docs worktree holds no change, so there is no docs PR to open", "edit the listed docs, or report noChange")]
            : await Findings(workspace, scope, changed.Value, cancellationToken).ConfigureAwait(false);
        return findings.Count == 0 ? StepFacts.Done("pass") : StepFacts.Done("fail", ("fact:findings", new JsonArray([.. findings])));
    }

    private async ValueTask<List<JsonNode>> Findings(string workspace, JsonObject scope, IReadOnlyList<string> changed, CancellationToken cancellationToken)
    {
        var globs = (scope["paths"] as JsonArray ?? []).Select(static g => (string?)g).OfType<string>().ToList();
        var named = (scope["candidates"] as JsonArray ?? []).OfType<JsonObject>().Where(static c => (bool?)c["designNamed"] == true).Select(static c => (string?)c["path"]).OfType<string>().ToHashSet(StringComparer.Ordinal);
        List<JsonNode> findings = [];
        foreach (var path in changed)
        {
            if (!named.Contains(path) && !globs.Exists(g => DocGlob.Matches(g, path)))
            {
                findings.Add(DocsFinding.Of("DOCS-PATH-001", path, $"{path} is not a doc path", "keep the docs commit to the profile's doc paths and the docs the design names; revert this file"));
            }
            else if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                findings.AddRange(await Markdown(workspace, path, cancellationToken).ConfigureAwait(false));
            }
        }

        return findings;
    }

    private async ValueTask<List<JsonNode>> Markdown(string workspace, string path, CancellationToken cancellationToken)
    {
        var read = await files.Read(path, [workspace], cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return [];   // a deleted doc has nothing to check
        }

        List<JsonNode> findings = [];
        if (!MarkdownCheck.FencesBalanced(read.Value.Text))
        {
            findings.Add(DocsFinding.Of("DOCS-MD-001", path, $"{path} opens a code fence it never closes", "close the fence"));
        }

        foreach (var target in MarkdownCheck.FileLinks(read.Value.Text).Distinct(StringComparer.Ordinal))
        {
            if (!await Resolves(workspace, path, target, cancellationToken).ConfigureAwait(false))
            {
                findings.Add(DocsFinding.Of("DOCS-LINK-001", path, $"{path} links to {target}, which is not in the docs worktree", "fix the link or add the file"));
            }
        }

        return findings;
    }

    private async ValueTask<bool> Resolves(string workspace, string from, string target, CancellationToken cancellationToken)
    {
        if (target.EndsWith('/'))
        {
            return true;   // a directory link: the reader names files only
        }

        var resolved = Resolve(from, target);
        return resolved is not null && (await files.Read(resolved, [workspace], cancellationToken).ConfigureAwait(false)).IsSuccess;
    }

    // The link's path from the repo root; null when it climbs out of it.
    private static string? Resolve(string from, string target)
    {
        var parts = target.StartsWith('/') ? [] : from.Split('/')[..^1].ToList();
        foreach (var part in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
            }
            else if (!string.Equals(part, ".", StringComparison.Ordinal))
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }
}
