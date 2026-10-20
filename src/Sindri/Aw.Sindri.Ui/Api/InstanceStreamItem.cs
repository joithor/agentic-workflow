namespace Aw.Sindri.Ui.Api;

/// <summary>What the instance-change stream yields: that it is open (the first item, <see cref="Change"/> null), then a change per frame.</summary>
/// <param name="Change">The frame, or null for "the stream is open now".</param>
internal sealed record InstanceStreamItem(InstanceChange? Change)
{
    /// <summary>True for the signal that the connection is open (frames sent before it are lost, so the caller re-reads once it is).</summary>
    internal bool IsOpened => Change is null;
}
