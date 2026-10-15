namespace Aw.Meta.Core.Logs;

/// <summary>What a running-log line records.</summary>
public enum LogKind
{
    /// <summary>A pull request merged.</summary>
    Merge = 1,

    /// <summary>A decision.</summary>
    Decision = 2,

    /// <summary>The lane map changed.</summary>
    Lanes = 3,

    /// <summary>Something went wrong or was let through.</summary>
    Incident = 4,

    /// <summary>Anything else worth keeping.</summary>
    Note = 5,
}
