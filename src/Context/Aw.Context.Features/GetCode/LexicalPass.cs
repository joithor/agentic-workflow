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

    /// <summary>The source whose step is running now, or null between steps.</summary>
    internal SourceId? Running { get; set; }

    /// <summary>True while another repo is still to be searched after the current one.</summary>
    internal bool MoreRepos { get; set; }

    private bool TestsTimedOut { get; set; }

    private bool SchemaTimedOut { get; set; }

    internal bool TestsStarted { get; set; }

    internal bool SchemaStarted { get; set; }

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

    /// <summary>Notes that the deadline passed: the step running is cut, and so is any step still to run; a step that finished stays as it was.</summary>
    internal void Expire()
    {
        TestsTimedOut = Running != SchemaId || MoreRepos;
        SchemaTimedOut = true;
    }

    internal CodeLexicalFindings Findings(DateTimeOffset now) =>
        new(Tests, Schema, Searched, Unlocated, Capped)
        {
            TestsTruncated = TestsCapped,
            SchemaTruncated = SchemaCapped,
            TestsRow = Row(TestsId, new RowFacts(_testsFailure, Capped || TestsCapped, TestsStarted, Configured: true, TestsTimedOut), now),
            SchemaRow = Row(SchemaId, new RowFacts(_schemaFailure, SchemaCapped, SchemaStarted, Settings.SchemaGlobs.Count > 0, SchemaTimedOut), now),
        };

    private readonly record struct RowFacts((string Code, bool NotHeld)? Failure, bool Capped, bool Started, bool Configured, bool TimedOut);

    private static SearchedSource Row(SourceId id, RowFacts facts, DateTimeOffset now) =>
        (facts.Configured, facts.Failure, facts.TimedOut) switch
        {
            (false, _, _) => SearchedSources.Lexical(id, SearchedState.NotConfigured, "not-configured", code: null, asOf: null),
            (_, { NotHeld: true } f, _) => SearchedSources.Lexical(id, SearchedState.Unavailable, "commit-not-held", f.Code, asOf: null),
            (_, { } f, _) => SearchedSources.Lexical(id, SearchedState.Degraded, reason: null, f.Code, asOf: null),
            (_, _, true) => SearchedSources.Lexical(id, SearchedState.Degraded, reason: null, ContextErrors.CodeSearchTimedOut.Code, facts.Started ? now : null),   // a step that never began read nothing: no asOf
            _ => SearchedSources.Lexical(id, SearchedState.Ok, facts.Capped ? "capped" : null, code: null, now),
        };
}
