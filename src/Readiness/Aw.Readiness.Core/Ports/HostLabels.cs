namespace Aw.Readiness.Core.Ports;

/// <summary>The labels defined on the repo, or unknown.</summary>
/// <param name="Names">Label names, or null when they couldn't be read.</param>
/// <param name="Evidence">Where they came from, or why they couldn't be read.</param>
public sealed record HostLabels(IReadOnlyList<string>? Names, string Evidence);
