using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Stack;

/// <summary>
/// Reads profile emails from the local database with psql inside postgres_container. The seed pattern travels as a psql
/// variable (-v pattern=…) and the SQL on standard input, where psql interpolates :'pattern' as a quoted literal; psql
/// doesn't interpolate variables in a -c command, so the pattern never becomes part of the SQL text.
/// </summary>
internal sealed class DockerPsqlProvenance(string docker) : IProvenanceQuery
{
    private const string Like = "SELECT email FROM public.profile WHERE email LIKE :'pattern';\n";
    private const string All = "SELECT email FROM public.profile;\n";

    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(15);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> EmailsLikeAsync(string likePattern, CancellationToken cancellationToken) =>
        QueryAsync(Like, ["-v", "pattern=" + likePattern], cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> AllEmailsAsync(CancellationToken cancellationToken) =>
        QueryAsync(All, [], cancellationToken);

    // docker exec -i (standard input carries the SQL), unaligned tuples only, stop on the first error.
    private IReadOnlyList<string> Command(IReadOnlyList<string> variables) =>
        [docker, "exec", "-i", "postgres_container", "psql", "-U", "postgres", "-At", "-v", "ON_ERROR_STOP=1", .. variables];

    private async ValueTask<Result<IReadOnlyList<string>>> QueryAsync(string sql, IReadOnlyList<string> variables, CancellationToken cancellationToken)
    {
        var run = await ProcessRunner.RunAsync(Command(variables), sql, _limit, EvidenceErrors.ProbeFailed, cancellationToken).ConfigureAwait(false);
        return run.Bind(outcome => outcome.ExitCode == 0
            ? Results.Success<IReadOnlyList<string>>([.. outcome.StandardOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)])
            : EvidenceErrors.ProbeFailed.With(string.Create(CultureInfo.InvariantCulture, $"psql failed (exit {outcome.ExitCode}): {outcome.StandardError.Trim()}")));
    }
}
