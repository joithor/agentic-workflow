namespace Aw.Sindri.Core.BugFix;

/// <summary>The refusals a Step branches on; every other refusal is <see cref="Other"/> and parks the instance with the reason.</summary>
public enum RefusalKind
{
    /// <summary>Any other refusal.</summary>
    Other = 0,

    /// <summary>No new attempt: the cap is used up.</summary>
    AttemptCap = 1,

    /// <summary>The candidate cannot be judged because its check run did not pass.</summary>
    NoPassingRun = 2,
}
