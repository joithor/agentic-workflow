namespace Aw.Meta.Core.Merge;

/// <summary>One merge precondition and whether it holds.</summary>
/// <param name="Name">The check name.</param>
/// <param name="Ok">Whether it holds.</param>
/// <param name="Detail">Why, in a line.</param>
public sealed record MergeCheck(string Name, bool Ok, string Detail);
