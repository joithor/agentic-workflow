namespace Aw.Meta.Core.Messages;

/// <summary>How a reply obligation was set aside without a reply.</summary>
public enum MessageResolutionKind
{
    /// <summary>Closed: no reply needed.</summary>
    Dismissed = 1,

    /// <summary>Snoozed until a time.</summary>
    Deferred = 2,
}
