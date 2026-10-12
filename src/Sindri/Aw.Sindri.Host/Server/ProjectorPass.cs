namespace Aw.Sindri.Host.Server;

/// <summary>How one projector pass ended.</summary>
internal enum ProjectorPass
{
    /// <summary>The views reached the ledger head seen at the start of the pass.</summary>
    CaughtUp,

    /// <summary>The projector is stopped on an event (SND-VIEW-001); it retries every 30 seconds, or on a rebuild.</summary>
    Stalled,

    /// <summary>The ledger or views.db couldn't be read or written; the next pass tries again.</summary>
    Failed,
}
