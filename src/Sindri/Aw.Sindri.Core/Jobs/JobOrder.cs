namespace Aw.Sindri.Core.Jobs;

/// <summary>The one order of jobs (API P6): by when each started, never by its latest activity, with the job id breaking a tie.</summary>
public static class JobOrder
{
    /// <summary>Orders <paramref name="rows"/> oldest start first (the order of the running list).</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, oldest start first, ties by id.</returns>
    public static IReadOnlyList<JobRow> OldestFirst(IEnumerable<JobRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.OrderBy(static r => r.StartedAt).ThenBy(static r => r.Id.Value, StringComparer.Ordinal)];
    }

    /// <summary>Orders <paramref name="rows"/> newest start first (the order of the jobs list).</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, newest start first, ties by id.</returns>
    public static IReadOnlyList<JobRow> NewestFirst(IEnumerable<JobRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.OrderByDescending(static r => r.StartedAt).ThenBy(static r => r.Id.Value, StringComparer.Ordinal)];
    }
}
