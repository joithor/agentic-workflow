using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The command line's frozen streams (TS cli.ts): a command produces an exit code, stdout and stderr; each stream gets
/// its text plus "\n" only when the text isn't empty, as UTF-8. Stdin is read whole and decoded as node decodes it
/// (invalid UTF-8 becomes U+FFFD). A test captures all three for its own async flow (<see cref="Capture"/>).
/// </summary>
internal static class CommandOutput
{
    private static readonly AsyncLocal<CommandCapture?> _capture = new();

    /// <summary>Captures stdin, stdout and stderr for the calling async flow, instead of the process's streams.</summary>
    internal static CommandCapture Capture(string stdin) => _capture.Value = new CommandCapture(stdin);

    /// <summary>Writes the frozen output and returns the exit code.</summary>
    internal static int Emit(int exitCode, string stdout, string stderr = "")
    {
        Write(stdout, error: false);
        Write(stderr, error: true);
        return exitCode;
    }

    /// <summary>Reads stdin to the end.</summary>
    internal static async Task<string> ReadInput(CancellationToken cancellationToken)
    {
        if (_capture.Value is { } capture)
        {
            return capture.Stdin;
        }

        var stdin = Console.OpenStandardInput();
        await using (stdin.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stdin.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
    }

    /// <summary>The scaffold's mapping (/new-slice generates calls to it): the value as Kernel JSON, or the error.</summary>
    internal static int Write<T>(Result<T> result, JsonSerializerOptions json) =>
        result.IsError
            ? Emit(1, "", Encoding.UTF8.GetString(AwJson.Serialize(result.Error, json)))
            : Emit(0, Encoding.UTF8.GetString(AwJson.Serialize(result.Value, json)));

    /// <summary>The scaffold's usage error (/new-slice generates calls to it).</summary>
    internal static int Usage(string message) => Emit(2, "", $"usage: {message}");

    private static void Write(string text, bool error)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_capture.Value is { } capture)
        {
            _ = (error ? capture.Stderr : capture.Stdout).Append(text).Append('\n');
            return;
        }

        using var stream = error ? Console.OpenStandardError() : Console.OpenStandardOutput();
        stream.Write(Encoding.UTF8.GetBytes(text + "\n"));
        stream.Flush();
    }
}
