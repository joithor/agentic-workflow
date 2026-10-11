using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// Output and exit codes for every command. stdout carries one JSON document (markdown for the comment command);
/// stderr carries one-line messages prefixed 'ui-evidence: '. Exit codes: 0 ok · 1 usage, invalid input or a guard
/// refusal · 2 a check failed · 3 blocked on review · 4 the run itself errored · 75 a lock stayed busy.
/// </summary>
internal static class CommandOutput
{
    internal const int Ok = 0;
    internal const int Refused = 1;
    internal const int CheckFailed = 2;
    internal const int ReviewBlocked = 3;
    internal const int RunErrored = 4;
    internal const int LockBusy = 75;
    internal const int Interrupted = 130;
    private const string Prefix = "ui-evidence: ";

    /// <summary>Writes <paramref name="value"/> as one JSON document and returns <paramref name="exit"/>.</summary>
    internal static int Json<T>(T value, JsonSerializerOptions json, int exit = Ok)
    {
        Console.Out.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(value, json)));
        return exit;
    }

    internal static int Markdown(string markdown)
    {
        Console.Out.WriteLine(markdown);
        return Ok;
    }

    // One line whatever the text carries (a step target, a path, a JSON value): callers parse stderr line by line.
    internal static void Message(string message) => Console.Error.WriteLine(Prefix + message.ReplaceLineEndings(" "));

    /// <summary>Writes the error to stderr; a lock timeout exits 75, an unavailable browser, file or lock directory 4, anything else 1.</summary>
    internal static int Error(Error error)
    {
        Message($"{error.Code} {error.Message}");
        if (error.Kind == ErrorKind.Timeout)
        {
            return LockBusy;
        }

        return error.Kind == ErrorKind.Unavailable ? RunErrored : Refused;
    }

    internal static int Usage(string shape)
    {
        Message($"usage: ui-evidence {shape}");
        return Refused;
    }
}
