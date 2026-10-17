using Aw.Meta.Core.Gate;

namespace Aw.Meta.Core.Coordination;

/// <summary>What a train gate is built on: the main every gate of the train pins, and the pull requests merged under it.</summary>
/// <param name="Main">The origin/main sha to build on; null to resolve origin/main.</param>
/// <param name="Stack">The pull requests ahead of this one with their heads, in merge order.</param>
public sealed record GateBase(string? Main, IReadOnlyList<StackedPr> Stack);
