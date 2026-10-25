using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>Turns one review decision into override rows (S-4): checks it still applies to the latest draft (SND-CONTEXT-047) and writes nothing itself. Pure.</summary>
public static partial class ReviewPlanner
{
    /// <summary>Plans the rows for a decision.</summary>
    /// <param name="decision">What the person decided.</param>
    /// <param name="draft">The latest draft as built (before overrides).</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <returns>The rows to append, or SND-CONTEXT-047 when the target is gone or decided already.</returns>
    public static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at) =>
        Plan(decision, new ReviewBasis(draft, overrides), who, at);

    /// <summary>Plans the rows for a decision against a basis (the latest draft, or the current map when a link or an undo is decided with no draft).</summary>
    /// <param name="decision">What the person decided.</param>
    /// <param name="basis">The map as the caller may see it, before overrides.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <returns>The rows to append, or SND-CONTEXT-047 when the target is gone or decided already.</returns>
    public static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, ReviewBasis basis, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(basis);
        return World.Of(basis, who, at).Plan(decision);
    }

    /// <summary>Plans a whole review file's decisions, each on top of the rows planned before it, against one build of the map.</summary>
    /// <param name="decisions">The decisions, in file order.</param>
    /// <param name="basis">The map as the caller may see it, before overrides.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Stops the plan between decisions.</param>
    /// <returns>The rows to append and the skipped lines (one per decision that is stale).</returns>
    /// <remarks>
    /// The same rows and skipped lines as planning each decision alone on top of the rows before it, without rebuilding the map per
    /// decision: an accept or a reject only takes entries out of the queue (its own; a node's reject also the links that end at it), so
    /// those are claimed in the one build. Any other verb plans against a fresh build with every row so far, as a single call would.
    /// </remarks>
    public static (IReadOnlyList<MapOverride> Rows, IReadOnlyList<string> Skipped) PlanAll(IReadOnlyList<ReviewDecision> decisions, ReviewBasis basis, string who, DateTimeOffset at, CancellationToken cancellationToken) =>
        PlanAll(decisions, basis, (who, at), built: null, cancellationToken);

    /// <summary><see cref="PlanAll(IReadOnlyList{ReviewDecision}, ReviewBasis, string, DateTimeOffset, CancellationToken)"/>, calling <paramref name="built"/> once per build of the map (the size test counts them).</summary>
    internal static (IReadOnlyList<MapOverride> Rows, IReadOnlyList<string> Skipped) PlanAll(IReadOnlyList<ReviewDecision> decisions, ReviewBasis basis, (string Who, DateTimeOffset At) by, Action? built, CancellationToken cancellationToken)
    {
        var (who, at) = by;
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(basis);
        var rows = new List<MapOverride>();
        var skipped = new List<string>();
        World? world = null;
        ReviewBasis Current() => basis with { Overrides = [.. basis.Overrides, .. rows] };

        foreach (var decision in decisions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result<IReadOnlyList<MapOverride>> planned;
            if (decision.Verb is ReviewVerb.Accept or ReviewVerb.Reject)
            {
                world = world is { CanClaim: true } ? world : World.Of(Counted(built, Current()), who, at);
                planned = PlanAndClaim(world, decision);
            }
            else
            {
                world = null;   // an edit can change any entry: the next decision plans against a fresh build
                planned = Plan(decision, Counted(built, Current()), who, at);
            }

            if (planned.IsError)
            {
                skipped.Add(planned.Error.Message);
            }
            else
            {
                rows.AddRange(planned.Value);
            }
        }

        return (rows, skipped);
    }

    // A basis about to be built into a map, counted.
    private static ReviewBasis Counted(Action? built, ReviewBasis basis)
    {
        built?.Invoke();
        return basis;
    }

    // Plans an accept or a reject on the world and takes what its rows decide out of that world's queue.
    private static Result<IReadOnlyList<MapOverride>> PlanAndClaim(World world, ReviewDecision decision)
    {
        var planned = world.Plan(decision);
        foreach (var row in planned.IsSuccess ? planned.Value : [])
        {
            world.Claim(row);
        }

        return planned;
    }

    private static Result<IReadOnlyList<MapOverride>> One(Result<MapOverride> row) => row.Map<IReadOnlyList<MapOverride>>(r => [r]);

    private static Error Stale(string message) => ContextErrors.ReviewTargetStale.With(message);
}
