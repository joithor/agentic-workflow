using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetCode;

/// <summary>The schema rule: the repo's files that match the profile's schema globs and name a data thing (any form of its key name, label or aliases).</summary>
internal sealed class LexicalSchemaRule(LexicalCandidates candidates)
{
    internal async ValueTask Find(LexicalRun run, CancellationToken cancellationToken)
    {
        var pass = run.Pass;
        if (pass.Settings.SchemaGlobs.Count == 0)
        {
            return;
        }

        var schemaPaths = run.Tracked.Where(p => PathDenylist.Matches(p, pass.Settings.SchemaGlobs)).ToList();
        foreach (var thing in pass.Things.Where(t => t.Searchable).Take(CodeRules.MaxThings))
        {
            var terms = CodeLensBuilder.ThingTerms(pass.Graph, thing.Key, pass.Clean);
            if (terms.Count == 0 || schemaPaths.Count == 0)
            {
                continue;
            }

            var query = new CandidateQuery(terms, schemaPaths, CodeRules.MaxFilesPerThing + 1, LexicalPass.SchemaId);
            if (await candidates.Find(run, query, cancellationToken).ConfigureAwait(false) is not { } found)
            {
                return;
            }

            pass.SchemaCapped |= found.Cut;
            await Name(run, thing, found.Paths, terms, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask Name(LexicalRun run, CodeThing thing, List<string> paths, IReadOnlyList<string> terms, CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            var text = await candidates.Text(run, path, cancellationToken).ConfigureAwait(false);
            if (text is not null && NameMatcher.FindTerm(text, terms) is { } matched)
            {
                run.Pass.Schema.Add(new LexicalSchemaFile(thing.Key, path, matched));
            }
        }
    }
}
