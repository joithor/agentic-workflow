namespace Aw.Meta.Core.Usage;

/// <summary>Whether a lane or the meta can work now.</summary>
public enum UsageState
{
    /// <summary>No usage-limit stop is the last thing it shows.</summary>
    Ok = 1,

    /// <summary>It stopped at a usage limit.</summary>
    Limited = 2,

    /// <summary>Neither its pane nor its transcript could be read.</summary>
    Unknown = 3,
}
