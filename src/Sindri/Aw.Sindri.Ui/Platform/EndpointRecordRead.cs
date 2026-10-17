namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// <see cref="IEndpointRecordSource.Read"/>'s outcome: the record's bytes, no record, or a record the platform can't
/// read with <see cref="Failure"/> saying why in the port's terms (an expected failure, so a value and not an exception:
/// csharp.md "Errors"). The app words the reason itself, so it never reads a platform's own exception types (they differ,
/// and their messages name the full path).
/// </summary>
public sealed class EndpointRecordRead
{
    private EndpointRecordRead(byte[]? bytes, EndpointRecordFailure? failure)
    {
        Bytes = bytes;
        Failure = failure;
    }

    /// <summary>Gets the record's JSON bytes; null when there is none or it can't be read (<see cref="Failure"/> tells which).</summary>
#pragma warning disable CA1819 // the record's bytes are handed to the parser as read, without a copy
    public byte[]? Bytes { get; }
#pragma warning restore CA1819

    /// <summary>Gets why a record that is there can't be read; null when it could be, or there is none.</summary>
    public EndpointRecordFailure? Failure { get; }

    /// <summary>Gets the outcome of a source with no record.</summary>
    public static EndpointRecordRead None { get; } = new(bytes: null, failure: null);

    /// <summary>A record that was read.</summary>
    /// <param name="bytes">The record's JSON bytes.</param>
    /// <returns>The outcome.</returns>
    public static EndpointRecordRead Found(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new(bytes, failure: null);
    }

    /// <summary>A record that is there but can't be read.</summary>
    /// <param name="why">Why not.</param>
    /// <returns>The outcome.</returns>
    public static EndpointRecordRead Unreadable(EndpointRecordFailure why) => new(bytes: null, why);
}
