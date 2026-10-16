namespace Aw.Meta.Core.Gate;

/// <summary>Whether a red test is the pull request's fault.</summary>
public enum FlakeVerdict
{
    /// <summary>It passes alone, or fails the same way on plain main: not caused by the pull request.</summary>
    Flake = 1,

    /// <summary>It fails alone and not the same way on main.</summary>
    Real = 2,
}
