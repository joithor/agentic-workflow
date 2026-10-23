namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the ProjectUpdates operation.</summary>
/// <param name="ProjectUpdates">The ProjectUpdates Linear serves.</param>
internal sealed record LinearUpdatesData(LinearConnection<LinearProjectUpdate> ProjectUpdates);
