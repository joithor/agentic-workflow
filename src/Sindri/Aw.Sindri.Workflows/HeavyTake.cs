namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'TakeHeavy' is asked (LN-WF3): take a heavy-job slot for an instance, waiting at most <paramref name="Bound"/>.</summary>
/// <param name="Address">The instance's address; the lease is kept under it and the slot's owner file names it.</param>
/// <param name="Bound">How long to wait for a slot: the time left on the state's timer.</param>
/// <param name="Attempt">The instance's take number: a release names the last one it covers, so a take it cancelled, still running on the worker, gives its slot back instead of keeping it.</param>
internal sealed record HeavyTake(string Address, TimeSpan Bound, int Attempt);
