using System.Globalization;
using System.Runtime.CompilerServices;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the frames of a 'text/event-stream' body (API spec §5.3): 'id:', 'event:' and 'data:' lines up to a blank line; ': ' comments (the ping) are dropped.</summary>
internal static class SseFrames
{
    /// <summary>The frames of <paramref name="reader"/> until the stream ends or breaks.</summary>
    /// <param name="reader">The body.</param>
    /// <param name="ct">Ends the read.</param>
    /// <returns>The frames, oldest first.</returns>
    public static async IAsyncEnumerable<StreamFrame> Read(StreamReader reader, [EnumeratorCancellation] CancellationToken ct)
    {
        string? id = null, name = null;
        var data = string.Empty;
        while (await Line(reader, ct).ConfigureAwait(false) is { } line)
        {
            if (line.Length > 0)
            {
                (id, name, data) = Field(line, id, name, data);
            }
            else if (Complete(id, name, data) is { } frame)
            {
                yield return frame;
                (id, name, data) = (null, null, string.Empty);
            }
        }
    }

    private static (string? Id, string? Name, string Data) Field(string line, string? id, string? name, string data) =>
        line.StartsWith("id: ", StringComparison.Ordinal) ? (line[4..], name, data)
        : line.StartsWith("event: ", StringComparison.Ordinal) ? (id, line[7..], data)
        : line.StartsWith("data: ", StringComparison.Ordinal) ? (id, name, line[6..])
        : (id, name, data);   // a comment, such as ': ping', or a field this reader doesn't use

    /// <summary>The frame the lines so far make, or null (comments alone make none). The error frame carries no id.</summary>
    private static StreamFrame? Complete(string? id, string? name, string data) =>
        name is null ? null
        : name == "error" ? new StreamFrame(0, name, data)
        : long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ? new StreamFrame(sequence, name, data)
        : null;

    /// <summary>The next line; null when the stream ended, the connection broke or the server went away.</summary>
    private static async Task<string?> Line(StreamReader reader, CancellationToken ct)
    {
        try
        {
            return await reader.ReadLineAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or HttpRequestException or ObjectDisposedException)
        {
            return null;
        }
    }
}
