namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// <see cref="IEndpointRecordSource.Read"/>'s one failure: a record is there, but the platform can't read it.
/// <see cref="Reason"/> says why in the port's terms, so the app words it without reading the platform's own exceptions
/// (their types differ by platform, and their messages name the full path). A head's adapter maps its platform's errors
/// to it; anything else it throws is a bug.
/// </summary>
public sealed class EndpointRecordUnreadableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EndpointRecordUnreadableException"/> class (an unnamed I/O error).</summary>
    public EndpointRecordUnreadableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EndpointRecordUnreadableException"/> class (an unnamed I/O error).</summary>
    /// <param name="message">The message (never shown in the window).</param>
    public EndpointRecordUnreadableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EndpointRecordUnreadableException"/> class (an unnamed I/O error).</summary>
    /// <param name="message">The message (never shown in the window).</param>
    /// <param name="innerException">The platform's error.</param>
    public EndpointRecordUnreadableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EndpointRecordUnreadableException"/> class.</summary>
    /// <param name="reason">Why it can't be read.</param>
    /// <param name="innerException">The platform's error, if any.</param>
    public EndpointRecordUnreadableException(EndpointRecordFailure reason, Exception? innerException)
        : base($"the endpoint record can't be read ({reason})", innerException)
    {
        Reason = reason;
    }

    /// <summary>Why it can't be read.</summary>
    public EndpointRecordFailure Reason { get; }
}
