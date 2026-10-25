namespace Aw.Sindri.Core.Scheduling;

/// <summary>Promote the held draft.</summary>
/// <param name="Reason">Why it goes ahead.</param>
/// <param name="StaleRepos">The repos the gate was waiting on that are not built at this moment (ordinal order); empty for <see cref="MapPublishReason.IndexReady"/> and <see cref="MapPublishReason.NoIndex"/>.</param>
public sealed record MapPublish(MapPublishReason Reason, IReadOnlyList<string> StaleRepos) : MapPublishAction;
