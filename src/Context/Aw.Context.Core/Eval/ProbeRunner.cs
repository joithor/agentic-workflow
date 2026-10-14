using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Eval;

/// <summary>
/// Runs a probe file (spec §9.2 item 3, Phase 3): asks each question through a delegate (in Sindri, the CLI's GET
/// /sindri/context/query), scores the answers, and compares the means with the previous run of the same file.
/// </summary>
public static class ProbeRunner
{
    /// <summary>Runs <paramref name="file"/>.</summary>
    /// <param name="file">The checked probe file.</param>
    /// <param name="ask">Answers one probe's question; an error is scored 0 for that probe.</param>
    /// <param name="previous">The previous run of the same file, or null.</param>
    /// <param name="runAt">The time now.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report; the first error when every probe failed with one (the run says nothing then).</returns>
    public static ValueTask<Result<ProbeReport>> Run(ProbeFile file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, ProbeReport? previous,
        DateTimeOffset runAt, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Checks a probe file's content: at least one probe, ids unique slugs, questions 1 to 2000 characters, schema version 1.</summary>
    /// <param name="set">The parsed file.</param>
    /// <returns>Null when valid, else SND-CONTEXT-035 naming the key path.</returns>
    public static Error? Check(ProbeSet set) => throw new NotSupportedException();
}
