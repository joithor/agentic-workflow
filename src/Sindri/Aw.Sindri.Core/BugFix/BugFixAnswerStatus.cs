namespace Aw.Sindri.Core.BugFix;

/// <summary>How a bugfix-state call ended.</summary>
public enum BugFixAnswerStatus
{
    /// <summary>It did what was asked.</summary>
    Ok = 1,

    /// <summary>A rule of the bug fix declined it.</summary>
    Refused = 2,

    /// <summary>The tool could not do its job: missing, timed out, or it printed something unreadable.</summary>
    Unavailable = 3,
}
