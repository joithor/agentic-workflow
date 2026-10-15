namespace Aw.Meta.Core.Messages;

/// <summary>What an inbound message is.</summary>
public enum MessageKind
{
    /// <summary>A request or any other message.</summary>
    Request = 1,

    /// <summary>A question for the owner; the meta must queue it and acknowledge.</summary>
    OwnerQuestion = 2,

    /// <summary>A lane saying a pull request is ready to merge.</summary>
    Ready = 3,

    /// <summary>A lane announcing itself.</summary>
    LaneAnnounce = 4,
}
