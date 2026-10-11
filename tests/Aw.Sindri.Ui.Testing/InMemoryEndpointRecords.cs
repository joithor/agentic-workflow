using System.Globalization;
using System.Text;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory endpoint record: whatever bytes the test put there, or none.</summary>
internal sealed class InMemoryEndpointRecords : IEndpointRecordSource
{
    private byte[]? _bytes;

    public string Location => "memory:control.json";

    /// <summary>Writes a record the way the control plane does (camelCase JSON).</summary>
    /// <param name="record">The record.</param>
    public void Write(ControlRecord record) =>
        WriteText(string.Create(CultureInfo.InvariantCulture, $$"""{"url":"{{record.Url}}","pid":{{record.Pid}},"epoch":{{record.Epoch}}}"""));

    /// <summary>Writes raw text (a damaged record).</summary>
    /// <param name="text">The text.</param>
    public void WriteText(string text) => _bytes = Encoding.UTF8.GetBytes(text);

    /// <summary>Removes the record, as a clean stop does.</summary>
    public void Remove() => _bytes = null;

    public ValueTask<byte[]?> Read(CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(_bytes is null ? null : [.. _bytes]);
}
