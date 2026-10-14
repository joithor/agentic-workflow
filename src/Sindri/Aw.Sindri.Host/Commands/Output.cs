using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>Where a command writes: text or JSON on stdout, errors as 'CODE message' on stderr. No colour, so state is always a word.</summary>
internal sealed class Output(TextWriter stdout, TextWriter stderr, JsonSerializerOptions options)
{
    public TextWriter Out { get; } = stdout;

    public async Task<int> Json<T>(T value, int exitCode)
    {
        await Out.WriteLineAsync(Encoding.UTF8.GetString(AwJson.Serialize(value, options))).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>Prints an API response body exactly as received, then a newline (--json, API spec §9).</summary>
    /// <param name="body">The body.</param>
    /// <param name="exitCode">The exit code to return.</param>
    /// <returns><paramref name="exitCode"/>.</returns>
    public async Task<int> Raw(ReadOnlyMemory<byte> body, int exitCode)
    {
        await Out.WriteLineAsync(Encoding.UTF8.GetString(body.Span)).ConfigureAwait(false);
        return exitCode;
    }

    public async Task<int> Text(string text, int exitCode)
    {
        await Out.WriteAsync(text).ConfigureAwait(false);
        return exitCode;
    }

    /// <summary>
    /// Prints the error; exit 1 for 'not running' (attention), 2 for everything else. 'Not running' gets the fix
    /// 'sindri' unless it names one already, or asks for the human (an agent session never starts it).
    /// </summary>
    public async Task<int> Error(Error error)
    {
        var notRunning = error.Code == SindriErrors.NotRunning.Code;
        var named = error.Message.Contains("fix:", StringComparison.Ordinal) || error.Message.Contains("ask the human", StringComparison.Ordinal);
        var fix = notRunning && !named ? " (fix: sindri)" : string.Empty;
        await stderr.WriteLineAsync($"{error.Code} {error.Message}{fix}").ConfigureAwait(false);
        return notRunning ? 1 : 2;
    }
}
