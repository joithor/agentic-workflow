namespace Aw.Meta.Core.Obligations;

/// <summary>What the meta owes.</summary>
public enum ObligationKind
{
    /// <summary>A reply to an inbound message.</summary>
    Reply = 1,

    /// <summary>Sending the lane map to every lane.</summary>
    BroadcastLanes = 2,

    /// <summary>Telling every lane main moved.</summary>
    AnnounceMain = 3,

    /// <summary>Sending an owner's answer back to the lane that asked.</summary>
    RelayOwner = 4,

    /// <summary>Re-arming the watch.</summary>
    RearmWatch = 5,
}
