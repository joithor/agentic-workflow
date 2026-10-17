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
    /// <summary>The scaffold's mapping (/new-slice generates calls to it): the value as JSON, or the error.</summary>
    internal static int Write<T>(Result<T> result, JsonSerializerOptions json) =>
        result.IsError ? Error(result.Error, asJson: true, json) : Json(result.Value, passed: true, json);

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

    /// <summary>Says when a freeze found its hash already frozen by someone else: the first freeze's 'by' is kept, so this call's was not recorded.</summary>
    internal static void AlreadyFrozenNote(Core.Model.FreezeBy? firstFrozenBy)
    {
        if (firstFrozenBy is { } first)
        {
            var name = first == Core.Model.FreezeBy.Owner ? "owner" : "machine";
            Console.Out.WriteLine($"  note: alreadyFrozenBy {name}: this hash was already frozen by {name}; a freeze is write-once, so this call's --by was not recorded");
        }
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
        var failed = checks.Count(static c => c.State == CheckState.Fail) + checks.Sum(static c => c.Omitted);
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
        CheckState.Omitted => "FAIL …   ",
        _ => "unknown  ",
    };
}
