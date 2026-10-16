using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>A loopback server that answers every request with fixed bytes, or never answers, to reach replies a real framework would not produce.</summary>
internal sealed class RawHttpServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly byte[]? _response;

    internal RawHttpServer(byte[]? response)
    {
        _response = response;
        _listener.Start();
        _ = Task.Run(AcceptAsync, _stop.Token);
    }

    internal string Origin => "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static RawHttpServer Answering(string head, string body) =>
        new([.. Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\n{head}Content-Length: {Encoding.UTF8.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture)}\r\nConnection: close\r\n\r\n"), .. Encoding.UTF8.GetBytes(body)]);

    internal static RawHttpServer Silent() => new(response: null);

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = Task.Run(() => ServeAsync(client), _stop.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // disposed
        }
        catch (ObjectDisposedException)
        {
            // disposed
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var seen = new StringBuilder();
                while (!seen.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer, _stop.Token);
                    if (read == 0)
                    {
                        return;
                    }

                    _ = seen.Append(Encoding.Latin1.GetString(buffer, 0, read));
                }

                if (_response is null)
                {
                    await Task.Delay(Timeout.Infinite, _stop.Token);
                }
                else
                {
                    await stream.WriteAsync(_response, _stop.Token);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // the client went away or the server was disposed
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }
}
