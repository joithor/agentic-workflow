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

    [Fact]
    public async Task No_record_reads_as_null() => Expect.True(await With(null).Read(Token) is null);

    [Fact]
    public async Task A_record_reads_back_byte_for_byte()
    {
        byte[] record = [.. """{"url":"http://127.0.0.1:7410/","pid":1,"epoch":2}"""u8];

        Expect.Sequence(record, await With(record).Read(Token));
    }

    [Fact]
    public void It_names_where_it_reads() => Expect.True(!string.IsNullOrWhiteSpace(With(null).Location));
}
