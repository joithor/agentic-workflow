using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.GetCode;

/// <summary>What one lexical pass has found and how each of its two sources fared; the rows of 'searched' are built from it at the end.</summary>
/// <param name="settings">The checked settings (the globs, the deadline).</param>
/// <param name="graph">The graph the caller sees.</param>
/// <param name="things">The data things whose schema files are searched for.</param>
/// <param name="clean">Cleans one map name before it becomes a search term.</param>
internal sealed class LexicalPass(ContextSettings settings, GraphSnapshot graph, IReadOnlyList<CodeThing> things, Func<string, string> clean)
{
    internal static readonly SourceId TestsId = SourceId.From("lexical-tests");
    internal static readonly SourceId SchemaId = SourceId.From("lexical-schema");

    private (string Code, bool NotHeld)? _testsFailure;
    private (string Code, bool NotHeld)? _schemaFailure;

    internal ContextSettings Settings { get; } = settings;

    internal GraphSnapshot Graph { get; } = graph;

    internal IReadOnlyList<CodeThing> Things { get; } = things;

    internal Func<string, string> Clean { get; } = clean;

    internal List<LexicalTest> Tests { get; } = [];

    internal List<LexicalSchemaFile> Schema { get; } = [];

    internal List<CodeLexical> Searched { get; } = [];

    internal int Unlocated { get; set; }

    internal bool Capped { get; set; }

    internal bool TestsCapped { get; set; }

    internal bool SchemaCapped { get; set; }

    internal bool TimedOut { get; set; }

    /// <summary>Notes that a source (or both) could not be read; the first failure of each source is the one its row shows.</summary>
    internal void Fail(string code, bool commitNotHeld, SourceId? source)
    {
        if (source is null || source == TestsId)
        {
            _testsFailure ??= (code, commitNotHeld);
        }

        if (source is null || source == SchemaId)
        {
            _schemaFailure ??= (code, commitNotHeld);
        }
    }

    internal CodeLexicalFindings Findings(DateTimeOffset now) =>
        new(Tests, Schema, Searched, Unlocated, Capped)
        {
            TestsTruncated = TestsCapped,
            TestsRow = Row(TestsId, _testsFailure, Capped || TestsCapped, now, configured: true),
            SchemaRow = Row(SchemaId, _schemaFailure, SchemaCapped, now, Settings.SchemaGlobs.Count > 0),
        };

    private SearchedSource Row(SourceId id, (string Code, bool NotHeld)? failure, bool capped, DateTimeOffset now, bool configured) =>
        (configured, failure, TimedOut) switch
        {
            (false, _, _) => SearchedSources.Lexical(id, SearchedState.NotConfigured, "not-configured", code: null, asOf: null),
            (_, { NotHeld: true } f, _) => SearchedSources.Lexical(id, SearchedState.Unavailable, "commit-not-held", f.Code, asOf: null),
            (_, { } f, _) => SearchedSources.Lexical(id, SearchedState.Degraded, reason: null, f.Code, asOf: null),
            (_, _, true) => SearchedSources.Lexical(id, SearchedState.Degraded, reason: null, ContextErrors.CodeSearchTimedOut.Code, now),
            _ => SearchedSources.Lexical(id, SearchedState.Ok, capped ? "capped" : null, code: null, now),
        };
}
