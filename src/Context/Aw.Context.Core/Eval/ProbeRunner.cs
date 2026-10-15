using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Eval;

/// <summary>
/// Runs a probe file (spec §9.2 item 3, Phase 3): asks each question through a delegate (in Sindri, the CLI's GET
/// /sindri/context/query), scores the answers, and compares the means with the previous run of the same file.
/// </summary>
public static partial class ProbeRunner
{
    private const int MaxQuestion = 2_000;

    /// <summary>Runs <paramref name="file"/>.</summary>
    /// <param name="file">The checked probe file.</param>
    /// <param name="ask">Answers one probe's question; an error is scored 0 for that probe and makes the run incomplete (never a regression, never a baseline).</param>
    /// <param name="previous">The previous run of the same file, or null.</param>
    /// <param name="runAt">The time now.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report (<see cref="ProbeReport.Incomplete"/> when any probe failed); the first error when every probe failed with one.</returns>
    public static async ValueTask<Result<ProbeReport>> Run(ProbeFile file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, ProbeReport? previous,
        DateTimeOffset runAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(ask);
        var scores = new List<ProbeScore>();
        Error? firstError = null;
        string? mapVersion = null;
        foreach (var probe in file.Set.Probes)
        {
            var answer = await ask(probe, cancellationToken).ConfigureAwait(false);
            if (answer.IsError)
            {
                firstError ??= answer.Error;
                scores.Add(new ProbeScore(probe.Id, 0, 0, [], [], [], answer.Error.Code));
                continue;
            }

            mapVersion ??= answer.Value.MapVersion;
            scores.Add(ProbeScoring.Score(probe, answer.Value));
        }

        if (firstError is not null && scores.TrueForAll(s => s.Error is not null))
        {
            return firstError;
        }

        var totals = new ProbeTotals(ProbeScoring.Round(scores.Average(s => s.PrecisionAt5)), ProbeScoring.Round(scores.Average(s => s.CitationCorrectness)), scores.Count, runAt,
            scores.Sum(s => s.AbsentReturned.Count));
        var before = previous?.Totals;
        var report = new ProbeReport(ProbeReport.CurrentSchemaVersion, file.Path, file.Hash, mapVersion, scores, totals, before, Regressed: false);
        return report.Incomplete || before is null ? report : report with { Regressed = Worse(totals, before) };
    }

    private static bool Worse(ProbeTotals now, ProbeTotals before) =>
        now.PrecisionAt5 < before.PrecisionAt5 || now.CitationCorrectness < before.CitationCorrectness || now.AbsentReturned > before.AbsentReturned;

    /// <summary>Checks a probe file's content: at least one probe, ids unique slugs, questions 1 to 2000 characters, schema version 1.</summary>
    /// <param name="set">The parsed file.</param>
    /// <returns>Null when valid, else SND-CONTEXT-035 naming the key path.</returns>
    public static Error? Check(ProbeSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.SchemaVersion is { } version && version != 1)
        {
            return Invalid("schemaVersion", "this build reads probe files of version 1");
        }

        if (set.Probes is null || set.Probes.Count == 0)
        {
            return Invalid("probes", "needs at least one probe");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < set.Probes.Count; i++)
        {
            var probe = set.Probes[i];
            var at = string.Create(CultureInfo.InvariantCulture, $"probes[{i}]");
            var problem = probe switch
            {
                null => Invalid(at, "is empty"),
                { Expect: null } => Invalid(at + ".expect", "is required (an object, its lists optional)"),
                _ when !Slug.IsMatch(probe.Id.Value) => Invalid(at + ".id", "is a slug: lower-case letters, digits and '-', 1 to 100 characters"),
                _ when !seen.Add(probe.Id.Value) => Invalid(at + ".id", $"'{probe.Id.Value}' is already the id of an earlier probe"),
                _ when string.IsNullOrWhiteSpace(probe.Question) || probe.Question.Length > MaxQuestion => Invalid(at + ".question", "is 1 to 2000 characters"),
                _ => null,
            };
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error Invalid(string keyPath, string problem) => ContextErrors.ProbeFileInvalid.With($"{keyPath}: {problem}; nothing was run");

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Slug { get; }
}
