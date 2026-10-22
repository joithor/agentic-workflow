using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Health;

/// <summary>Folds the status inputs into one <see cref="HealthReport"/> (UI spec E-14; P2 plan G10): stalled &gt; rebuilding &gt; degraded &gt; healthy.</summary>
public static class HealthSummary
{
    private const string SchedulerCode = "SND-SCHED-001";

    /// <summary>The health for these inputs.</summary>
    /// <param name="running">Whether this process holds the lease.</param>
    /// <param name="views">The projector's status.</param>
    /// <param name="services">The services view's rows, or null when the views can't be read.</param>
    /// <param name="profileNeedsApproval">True when no profile is approved.</param>
    /// <param name="profileChanged">True when the live profile changed since approval.</param>
    /// <returns>The health.</returns>
    public static HealthReport For(bool running, ViewsStatus views, IReadOnlyList<ServiceRow>? services, bool profileNeedsApproval, bool profileChanged)
    {
        ArgumentNullException.ThrowIfNull(views);
        List<HealthProblem> problems = [];
        if (!running)
        {
            problems.Add(new("SND-API-001", "Sindri isn't running: run `sindri`", "Sindri isn't running"));
        }

        problems.AddRange(Views(views));
        if (services is null && !views.Stalled)
        {
            problems.Add(new("SND-VIEW-001", EmptyText.Unreadable(), "the views can't be read"));
        }

        problems.AddRange((services ?? []).Where(s => s.LastRun is { Outcome: ServiceOutcome.Failed }).Select(s => Failed(s.Name, s.LastRun)));
        if (profileChanged)
        {
            problems.Add(new("SND-PROFILE-003", "The live profile changed since approval: run `sindri profile approve`", "the profile changed since approval"));
        }
        else if (profileNeedsApproval)
        {
            problems.Add(new("SND-PROFILE-005", "Not approved: run `sindri profile approve`", "the profile isn't approved"));
        }

        var word = views.Stalled ? HealthWord.Stalled : views.Rebuilding ? HealthWord.Rebuilding : (problems.Count > 0 ? HealthWord.Degraded : HealthWord.Healthy);
        return new HealthReport(word, [.. problems], Sentence(word, problems));
    }

    /// <summary>A report that also counts the engine's stuck instances (API spec §3.8, E-30): any make a healthy report degraded, and join its problems and sentence.</summary>
    /// <param name="report">The report <see cref="For"/> folded.</param>
    /// <param name="stuck">The instances that only wait on a signal or update older than five minutes, with no timer.</param>
    /// <returns>The report; the same one when none are stuck.</returns>
    public static HealthReport WithStuck(HealthReport report, int stuck)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (stuck <= 0)
        {
            return report;
        }

        var clause = stuck == 1 ? "1 workflow instance is stuck waiting" : string.Create(CultureInfo.InvariantCulture, $"{stuck} workflow instances are stuck waiting");
        var problems = report.Problems.Append(new HealthProblem("SND-ENGINE-002", $"{clause} on a signal or update with no timer: open the engine screen", clause)).ToList();
        var word = report.Word == HealthWord.Healthy ? HealthWord.Degraded : report.Word;
        return new HealthReport(word, problems, Sentence(word, problems));
    }

    private static IEnumerable<HealthProblem> Views(ViewsStatus views)
    {
        if (views.Stalled)
        {
            yield return views.StalledAt is { } at
                ? new("SND-VIEW-001", EmptyText.Stalled(at), string.Create(CultureInfo.InvariantCulture, $"the views stopped at event {at}"))
                : new("SND-VIEW-001", EmptyText.Failing(views.StalledType), "the views can't be updated");
        }
        else if (views.Rebuilding)
        {
            yield return new("SND-API-010", EmptyText.Rebuilding(views.Cursor, views.LedgerHead),
                string.Create(CultureInfo.InvariantCulture, $"the views are replaying (cursor {views.Cursor} of {views.LedgerHead})"));
        }
    }

    private static HealthProblem Failed(ServiceName name, LastRun? run)
    {
        var slot = run?.Slot.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return new(run?.Code ?? SchedulerCode, $"Service {name.Value} failed at {slot}", $"service {name.Value} failed");
    }

    private static string? Sentence(HealthWord word, List<HealthProblem> problems)
    {
        if (problems.Count == 0)
        {
            return null;
        }

        var clauses = problems.ConvertAll(p => p.Clause);
        var joined = clauses.Count switch
        {
            1 => clauses[0],
            2 => $"{clauses[0]} and {clauses[1]}",
            _ => $"{string.Join(", ", clauses.Take(clauses.Count - 1))}, and {clauses[^1]}",
        };
        return $"Sindri is {word.ToString().ToLowerInvariant()}: {joined}.";
    }
}
