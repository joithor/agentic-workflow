using System.Globalization;
using System.Text;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes server-sent-event frames to a response body, flushing after every frame so a client sees each one as soon as it is
/// written (API spec §5.2, §5.5): an event frame (id, event, data), a comment line (the keep-alive ping) and the error frame.
/// TypedResults.ServerSentEvents can't write a comment or a hand-built error frame, hence this small writer.
/// A write to a client that has gone surfaces as the stream's exception or a cancellation; the caller ends the stream on it.
/// </summary>
/// <param name="body">The response body.</param>
internal sealed class SseWriter(Stream body)
{
    /// <summary>One event frame: 'event: &lt;name&gt;', 'id: &lt;id&gt;' and the data lines, ended by a blank line.</summary>
    /// <param name="id">The event id (the ledger sequence), or null for a frame with none.</param>
    /// <param name="name">The event name.</param>
    /// <param name="data">The data; a line break in it becomes more 'data:' lines.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>When the frame is written and flushed.</returns>
    public ValueTask Event(long? id, string name, string data, CancellationToken cancellationToken)
    {
        var frame = new StringBuilder("event: ").Append(name).Append('\n');
        if (id is { } sequence)
        {
            _ = frame.Append("id: ").Append(sequence.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        foreach (var line in data.Split('\n'))
        {
            _ = frame.Append("data: ").Append(line.TrimEnd('\r')).Append('\n');
        }

        return Write(frame.Append('\n').ToString(), cancellationToken);
    }

    /// <summary>A comment frame (': &lt;text&gt;'), which a client ignores; it keeps an idle stream from looking dead.</summary>
    /// <param name="text">The comment, on one line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>When the comment is written and flushed.</returns>
    public ValueTask Comment(string text, CancellationToken cancellationToken) => Write(": " + text + "\n\n", cancellationToken);

    private async ValueTask Write(string frame, CancellationToken cancellationToken)
    {
        await body.WriteAsync(Encoding.UTF8.GetBytes(frame), cancellationToken).ConfigureAwait(false);
        await body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
