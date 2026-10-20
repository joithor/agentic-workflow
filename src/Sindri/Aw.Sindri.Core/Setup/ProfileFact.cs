namespace Aw.Sindri.Core.Setup;

/// <summary>Where the profile stands.</summary>
/// <param name="Presence">Missing, unapproved, drifted or approved.</param>
public sealed record ProfileFact(ProfilePresence Presence);
