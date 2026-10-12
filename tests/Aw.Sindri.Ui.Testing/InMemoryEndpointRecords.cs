using System.Globalization;
using System.Text;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory endpoint record: whatever bytes the test put there, or none.</summary>
internal sealed class InMemoryEndpointRecords : IEndpointRecordSource
{
    private byte[]? _bytes;
    private Exception? _fail;

    /// <summary>Gets or sets where the record is said to live (a long temp path in the Not running card's test).</summary>
    public string Location { get; set; } = "memory:control.json";

    /// <summary>Writes a record the way the control plane does (camelCase JSON).</summary>
    /// <param name="record">The record.</param>
    public void Write(ControlRecord record) =>
        WriteText(string.Create(CultureInfo.InvariantCulture, $$"""{"url":"{{record.Url}}","pid":{{record.Pid}},"epoch":{{record.Epoch}}}"""));

    /// <summary>Writes raw text (a damaged record).</summary>
    /// <param name="text">The text.</param>
    public void WriteText(string text) => _bytes = Encoding.UTF8.GetBytes(text);

    /// <summary>Makes the next read throw, as a record the platform can't read does (a directory, no permission).</summary>
    /// <param name="error">The exception.</param>
    public void FailNextRead(Exception error) => _fail = error;

    /// <summary>Removes the record, as a clean stop does.</summary>
    public void Remove() => _bytes = null;

    public ValueTask<byte[]?> Read(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fail, value: null) is { } error)
        {
            throw error;
        }

        return ValueTask.FromResult<byte[]?>(_bytes is null ? null : [.. _bytes]);
    }
}
