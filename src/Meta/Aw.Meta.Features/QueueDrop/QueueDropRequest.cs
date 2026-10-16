namespace Aw.Meta.Features.QueueDrop;

/// <summary>The QueueDrop request: remove a pull request's merge-queue entry.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Reason">Why the entry is dropped; it goes into the running log.</param>
public sealed record QueueDropRequest(int Pr, string Reason);
