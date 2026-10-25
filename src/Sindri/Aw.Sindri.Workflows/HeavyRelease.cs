namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'ReleaseHeavy' is asked (LN-WF3): give back the slot held for an instance, and any take up to <paramref name="Attempt"/> that is still running.</summary>
/// <param name="Address">The instance's address.</param>
/// <param name="Attempt">The latest take number the instance started.</param>
internal sealed record HeavyRelease(string Address, int Attempt);
