using System.Text.RegularExpressions;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Guard;

/// <summary>
/// Best-effort detection of write controls: a click target (or a goto path, query dropped) whose words include a
/// write verb, alone or fused with a control suffix ('savebutton', 'deleteBtn'). camelCase and separators split words.
/// </summary>
internal static partial class WriteWords
{
    private static readonly HashSet<string> _verbs = new(StringComparer.Ordinal)
    {
        "save", "submit", "update", "delete", "remove", "create", "confirm", "publish", "send", "apply", "approve", "post", "add",
        "invite", "archive", "deactivate", "activate", "enable", "disable", "cancel", "reset", "assign", "import", "upload", "complete",
        "finish", "accept", "reject", "sync", "release", "merge", "logout", "signout", "register",
    };

    // The words a CLI argument may carry on top of the UI list: commands that change or remove something.
    private static readonly HashSet<string> _cliVerbs = new(StringComparer.Ordinal)
    {
        "push", "drop", "rm", "truncate", "destroy", "purge", "wipe", "kill", "commit", "put", "patch", "write", "rollback", "revert", "uninstall",
    };

    private static readonly string[] _controlSuffixes = ["btn", "button", "link", "action"];

    internal static bool LooksLikeWrite(ScriptStep step)
    {
        var label = step.Action switch
        {
            StepAction.Click => step.Target,
            StepAction.Goto => step.Target.Split('?')[0],
            StepAction.Fill or StepAction.ExpectVisible => null,
            _ => null,
        };
        return LabelLooksLikeWrite(label);
    }

    /// <summary>The words of a CLI argument that read as write verbs (the UI list plus the CLI verbs).</summary>
    internal static IEnumerable<string> CliWritesIn(string argument) => Words(argument).Where(static w => IsWriteWord(w) || _cliVerbs.Contains(w));

    internal static bool LabelLooksLikeWrite(string? label) => label is not null && Words(label).Any(IsWriteWord);

    internal static IEnumerable<string> Words(string target) =>
        NonWord.Split(CamelBoundary.Replace(target, "${lower} ${upper}").ToLowerInvariant()).Where(static w => w.Length > 0);

    private static bool IsWriteWord(string token) =>
        _verbs.Contains(token)
        || _controlSuffixes.Any(suffix => token.EndsWith(suffix, StringComparison.Ordinal) && _verbs.Contains(token[..^suffix.Length]));

    [GeneratedRegex("(?<lower>[a-z0-9])(?<upper>[A-Z])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex CamelBoundary { get; }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex NonWord { get; }
}
