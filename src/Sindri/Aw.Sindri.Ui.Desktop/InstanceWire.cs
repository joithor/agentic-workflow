using System.Net.Sockets;
using System.Text;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The single instance's wire format: one message per connection, UTF-8 text ending in one newline (at most
/// <see cref="InstanceMessages.MaxLength"/> characters, no newline inside); the holder answers one byte, 1 handled and
/// 0 refused. The sender keeps its side open until the answer, so the holder sees it leave.
/// </summary>
internal static class InstanceWire
{
    internal const byte Handled = 1;
    internal const byte Refused = 0;
    private const byte NewLine = (byte)'\n';
    private const int MaxBytes = InstanceMessages.MaxLength * 4;
    private static readonly UTF8Encoding _strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The message's bytes on the wire, or null when it can't be sent (empty, over the limit, a newline inside).</summary>
    /// <param name="message">The message.</param>
    /// <returns>The bytes.</returns>
    internal static byte[]? Encode(string message) =>
        message.Length is 0 or > InstanceMessages.MaxLength || message.Contains('\n', StringComparison.Ordinal)
            ? null
            : Encoding.UTF8.GetBytes(message + "\n");

    /// <summary>Reads one message: the text before the newline, or null when the sender ended without one, sent too much or not UTF-8.</summary>
    /// <param name="client">The sender's connection.</param>
    /// <param name="cancellationToken">Ends the read.</param>
    /// <returns>The message, or null.</returns>
    internal static async Task<string?> Read(Socket client, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await client.ReceiveAsync(buffer.AsMemory(total), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            var end = Array.IndexOf(buffer, NewLine, total, read);
            total += read;
            if (end >= 0)
            {
                return Decode(new ArraySegment<byte>(buffer, 0, end));
            }
        }

        return null;
    }

    /// <summary>Completes when the sender closes its side (or sends more), or <paramref name="cancellationToken"/> ends the watch.</summary>
    /// <param name="client">The sender's connection, its message already read.</param>
    /// <param name="cancellationToken">Ends the watch.</param>
    /// <returns>A task that completes then.</returns>
    internal static async Task Left(Socket client, CancellationToken cancellationToken)
    {
        try
        {
            _ = await client.ReceiveAsync(new byte[1], SocketFlags.None, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Watch over.
        }
    }

    /// <summary>Whether the sender has closed its side by now: its connection reads as ready with nothing more to send.</summary>
    /// <param name="client">The sender's connection, its message already read.</param>
    /// <returns>True when it has gone.</returns>
    internal static bool Gone(Socket client)
    {
        try
        {
            return client.Poll(0, SelectMode.SelectRead);
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException)
        {
            return true;
        }
    }

    // The message, or null when it is empty, over the limit or not UTF-8.
    private static string? Decode(ArraySegment<byte> bytes)
    {
        if (bytes.Count is 0 or > MaxBytes)
        {
            return null;
        }

        try
        {
            var text = _strict.GetString(bytes);
            return text.Length <= InstanceMessages.MaxLength ? text : null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
