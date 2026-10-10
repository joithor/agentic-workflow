using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core.Checks;

namespace Aw.Packets.App.Commands;

/// <summary>
/// Output and exit codes for every command: 0 when every check passed (unchecked rules don't fail), 1 when a check
/// failed or the packet is malformed (an 'invalid' error), 2 for a usage error or when the tool couldn't do its job
/// (missing packet, unreadable repository, unwritable state). Results go to stdout, errors to stderr; no colour.
/// </summary>
internal static class CommandOutput
{
    internal static int Error(Error error, bool asJson, JsonSerializerOptions json)
    {
        Console.Error.WriteLine(asJson
            ? Encoding.UTF8.GetString(AwJson.Serialize(error, json))
            : $"aw-packets: {error.Code} {error.Message}");
        return error.Kind == ErrorKind.Invalid ? 1 : 2;
    }

    internal static int Json<T>(T value, bool passed, JsonSerializerOptions json)
    {
        Console.Out.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(value, json)));
        return passed ? 0 : 1;
    }

    internal static void Checks(IReadOnlyList<CheckResult> checks)
    {
        foreach (var check in checks)
        {
            Console.Out.WriteLine($"{Label(check.State)} {check.Rule} {check.Subject}: {check.Message}");
            if (check.Fix is { } fix)
            {
                Console.Out.WriteLine($"  fix: {fix}");
            }
        }
    }

    internal static string Summary(IReadOnlyList<CheckResult> checks)
    {
        var failed = checks.Count(static c => c.State == CheckState.Fail);
        var open = checks.Count(static c => c.State == CheckState.Unchecked);
        return string.Create(CultureInfo.InvariantCulture, $"{(failed == 0 ? "PASS" : "FAIL")} ({failed} failed, {open} unchecked)");
    }

    internal static int Usage(string message)
    {
        Console.Error.WriteLine($"usage: aw-packets {message}");
        return 2;
    }

    private static string Label(CheckState state) => state switch
    {
        CheckState.Pass => "pass     ",
        CheckState.Fail => "FAIL     ",
        CheckState.Unchecked => "unchecked",
        _ => "unknown  ",
    };
}
