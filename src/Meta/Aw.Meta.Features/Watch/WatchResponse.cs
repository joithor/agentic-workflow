namespace Aw.Meta.Features.Watch;

/// <summary>The Watch response, once the watch has expired.</summary>
/// <param name="Polls">How many polls ran.</param>
/// <param name="Lines">How many lines were emitted.</param>
public sealed record WatchResponse(int Polls, int Lines);
