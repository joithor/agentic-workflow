using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>aw-packets, run as a program (deps.yaml Sindri to Packets, kind cli): the one canonical hash of a packet directory (scoping spec §8.3, P1).</summary>
internal static class PacketsCli
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    /// <summary>The packet directory's canonical hash ('sha256:…') from 'aw-packets hash --kind scoping &lt;dir&gt;'. The program is AW_PACKETS_BIN, else 'aw-packets' on the PATH.</summary>
    internal static async Task<Result<string>> Hash(string packetDirectory, Func<string, string?> environment, CancellationToken ct)
    {
        var program = environment("AW_PACKETS_BIN") is { Length: > 0 } given ? given : "aw-packets";
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "hash", "--kind", "scoping", packetDirectory })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return Unavailable(program, "it did not start");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var hash = (await output.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0 && hash.StartsWith("sha256:", StringComparison.Ordinal)
                ? hash
                : Unavailable(program, $"exit {process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}: {(await error.ConfigureAwait(false)).Trim()}");
        }
        catch (Win32Exception e)
        {
            return Unavailable(program, e.Message);   // boundary: the program is not installed
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Unavailable(program, "it did not answer in 60 s");
        }
    }

    private static Error Unavailable(string program, string why) =>
        SindriErrors.InvalidInput.With($"aw-packets hash failed ({program}: {why}) (fix: build aw-packets and put it on the PATH, or set AW_PACKETS_BIN to its path)");
}
