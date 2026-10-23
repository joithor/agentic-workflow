namespace Aw.Sindri.Host;

/// <summary>What is set up for the map, as the "no map yet" answer says it; every member the answer leaves out is null.</summary>
/// <param name="Repos">The repos the map is for.</param>
/// <param name="ApprovedAt">When the profile was approved.</param>
/// <param name="FirstRunStartedAt">When the first map build started.</param>
internal sealed record ApiProblemNoMap(IReadOnlyList<string>? Repos = null, DateTimeOffset? ApprovedAt = null, DateTimeOffset? FirstRunStartedAt = null);
