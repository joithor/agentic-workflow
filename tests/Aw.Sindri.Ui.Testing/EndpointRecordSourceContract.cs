using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract every <see cref="IEndpointRecordSource"/> passes: the in-memory one and each head's adapter.</summary>
public abstract class EndpointRecordSourceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A source with the given record bytes, or none.</summary>
    /// <param name="record">The bytes the control plane wrote, or null for no record.</param>
    /// <returns>The source.</returns>
    protected abstract IEndpointRecordSource With(byte[]? record);

    /// <summary>
    /// A source whose record is there but can't be read, for <paramref name="why"/> (a real directory, a file without read
    /// permission); null when the binding can't make that case here (no Unix modes on Windows, or running as root).
    /// </summary>
    /// <param name="why">The failure to make.</param>
    /// <returns>The source, or null.</returns>
    protected abstract IEndpointRecordSource? Unreadable(EndpointRecordFailure why);

    [Fact]
    public async Task No_record_reads_as_null() => Expect.True(await With(null).Read(Token) is null);

    [Fact]
    public async Task A_record_reads_back_byte_for_byte()
    {
        byte[] record = [.. """{"url":"http://127.0.0.1:7410/","pid":1,"epoch":2}"""u8];

        Expect.Sequence(record, await With(record).Read(Token));
    }

    // Review run 4 (architecture-strategist-0): the port says why in its own terms, so the app never reads the platform's
    // exception types (on macOS and Linux .NET throws UnauthorizedAccessException for a directory and for a chmod 000
    // file alike).
    [Theory]
    [InlineData(EndpointRecordFailure.NoPermission)]
    [InlineData(EndpointRecordFailure.NotAFile)]
    [InlineData(EndpointRecordFailure.Unreadable)]
    public async Task A_record_the_platform_cant_read_fails_with_why(EndpointRecordFailure why)
    {
        if (Unreadable(why) is not { } source)
        {
            return; // this binding can't make the case here (Unreadable says when)
        }

        EndpointRecordFailure? seen = null;
        try
        {
            _ = await source.Read(Token);
        }
        catch (EndpointRecordUnreadableException e)
        {
            seen = e.Reason;
        }

        Expect.Equal(why, seen);
    }

    [Fact]
    public void It_names_where_it_reads() => Expect.True(!string.IsNullOrWhiteSpace(With(null).Location));
}
