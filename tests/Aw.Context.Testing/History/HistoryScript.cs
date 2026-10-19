namespace Aw.Context.Testing.History;

/// <summary>
/// The one history every <see cref="FileHistoryContract"/> world holds: an initial commit, a commit touching other paths, a branch merged with a merge commit, and a last
/// commit that only touches a path nobody searches. Real git and the fake both build it from these constants.
/// </summary>
public static class HistoryScript
{
    /// <summary>The first commit's subject.</summary>
    public const string InitialSubject = "Initial";

    /// <summary>The second commit's subject (a squash-style number).</summary>
    public const string TouchSubject = "Touch b (#2)";

    /// <summary>The merge commit's subject.</summary>
    public const string MergeSubject = "Merge pull request #5 from fixture-labs/feat";

    /// <summary>The last commit's subject.</summary>
    public const string EditSubject = "Edit other";

    /// <summary>A path whose name starts with a dash.</summary>
    public const string DashPath = "-dash.txt";

    /// <summary>The first commit's committer date.</summary>
    public static DateTimeOffset InitialAt { get; } = new(2025, 3, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The second commit's committer date.</summary>
    public static DateTimeOffset TouchAt { get; } = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The side commit's committer date (not on the first-parent line).</summary>
    public static DateTimeOffset SideAt { get; } = new(2025, 8, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The merge commit's committer date.</summary>
    public static DateTimeOffset MergeAt { get; } = new(2025, 9, 22, 11, 0, 0, TimeSpan.Zero);

    /// <summary>The last commit's committer date.</summary>
    public static DateTimeOffset EditAt { get; } = new(2025, 11, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A planted token that sits in a commit body, assembled at run time so this file holds no token.</summary>
    public static string BodyToken { get; } = "FAKE-SECRET-" + "7788";
}
