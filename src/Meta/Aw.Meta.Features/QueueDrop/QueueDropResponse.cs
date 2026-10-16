namespace Aw.Meta.Features.QueueDrop;

/// <summary>The QueueDrop response.</summary>
/// <param name="Pr">The pull request whose entry was removed.</param>
/// <param name="Line">The line appended to the running log.</param>
public sealed record QueueDropResponse(int Pr, string Line);
