namespace Aw.Sindri.Core.Setup;

/// <summary>Where the profile stands against its approval.</summary>
public enum ProfilePresence
{
    /// <summary>There is no profile file.</summary>
    Missing,

    /// <summary>A profile that was never approved.</summary>
    Unapproved,

    /// <summary>Approved once, and the live profile has changed since.</summary>
    Drifted,

    /// <summary>The live profile is the approved one.</summary>
    Approved,
}
