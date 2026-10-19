using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'workflow.build.commands' key (line spec LN-WF9): each command has a kebab-case name used once, a program with no blank argument, a duration when it sets a timeout, and a known stage.</summary>
internal static class BuildRules
{
    private const int MaxName = 32;

    internal static Error? Check(BuildSettings? build)
    {
        var commands = build?.Commands ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < commands.Count; i++)
        {
            var problem = Command(commands[i], string.Create(CultureInfo.InvariantCulture, $"workflow.build.commands[{i}]"), seen);
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error? Command(BuildCommand command, string path, HashSet<string> seen)
    {
        if (!IsName(command.Name))
        {
            return Invalid(path + ".name", "is not a command name: kebab-case, 1 to 32 characters (for example typecheck)");
        }

        if (!seen.Add(command.Name))
        {
            return Invalid(path + ".name", $"'{command.Name}' is used twice");
        }

        if (command.Run is not { Count: > 0 } run || run.Any(string.IsNullOrWhiteSpace))
        {
            return Invalid(path + ".run", "is the program and its arguments, none blank (for example [npm, run, typecheck])");
        }

        if (command.Timeout is { } timeout && !RecipeDuration.TryFrom(timeout, out _))
        {
            return Invalid(path + ".timeout", $"'{timeout}' is not a duration: a whole number and m, h or d (for example 30m, 2h)");
        }

        return command.On is { } on && on is not (BuildCommand.Red or BuildCommand.Checks or BuildCommand.Green)
            ? Invalid(path + ".on", $"'{on}' is not a stage: red, checks or green")
            : null;
    }

    private static bool IsName(string? name) =>
        name is { Length: > 0 and <= MaxName } && char.IsAsciiLetterLower(name[0]) && name.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
