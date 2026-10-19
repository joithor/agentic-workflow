namespace Aw.Context.Core.History;

/// <summary>The fixed caps of the history lens (S-H decision 16; no profile key).</summary>
public static class HistoryRules
{
    /// <summary>Records, decisions and changes each cap here, after the cut.</summary>
    public const int ListCap = 100;

    /// <summary>The implementing files searched per anchor set.</summary>
    public const int MaxFiles = 50;

    /// <summary>The changes read per repo (one more than the list cap, so a cut is told).</summary>
    public const int ChangesPerRepo = ListCap + 1;

    /// <summary>A change's subject is cut at this many characters.</summary>
    public const int SubjectChars = 200;
}
