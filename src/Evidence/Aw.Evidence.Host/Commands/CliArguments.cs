using System.Globalization;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// Strictly parsed arguments: an unknown flag, a flag without its value, a repeated flag or the wrong number of
/// positionals is a usage error (null from <see cref="Parse"/>). A value never starts with '--'.
/// </summary>
internal sealed class CliArguments
{
    internal const string LockTimeoutFlag = "--lock-timeout";
    private const int DefaultLockTimeoutSeconds = 120;
    private const int MaxLockTimeoutSeconds = 86_400;

    private readonly List<string> _positionals = [];
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _switches = new(StringComparer.Ordinal);

    private CliArguments()
    {
    }

    internal string this[int index] => _positionals[index];

    internal static CliArguments? Parse(IReadOnlyList<string> args, CliSpec spec)
    {
        var parsed = new CliArguments();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var ok = arg.StartsWith("--", StringComparison.Ordinal)
                ? parsed.TakeFlag(args, ref i, spec)
                : arg.Length > 0 && parsed.TakePositional(arg, spec);
            if (!ok)
            {
                return null;
            }
        }

        return parsed._positionals.Count == spec.Positionals ? parsed : null;
    }

    internal string? Value(string flag) => _values.GetValueOrDefault(flag);

    internal bool Has(string flag) => _switches.Contains(flag);

    /// <summary>The flag's value made absolute against the working directory, when given.</summary>
    internal string? PathValue(string flag) => Value(flag) is { } path ? Path.GetFullPath(path) : null;

    /// <summary>The --lock-timeout seconds (default 120, 0 tries once); null when not a whole number in range.</summary>
    internal TimeSpan? LockTimeout() =>
        Value(LockTimeoutFlag) is not { } text
            ? TimeSpan.FromSeconds(DefaultLockTimeoutSeconds)
            : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds <= MaxLockTimeoutSeconds
                ? TimeSpan.FromSeconds(seconds)
                : null;

    private bool TakeFlag(IReadOnlyList<string> args, ref int i, CliSpec spec)
    {
        var flag = args[i];
        if (spec.Switches.Contains(flag, StringComparer.Ordinal))
        {
            return _switches.Add(flag);
        }

        if (!spec.ValueFlags.Contains(flag, StringComparer.Ordinal) || _values.ContainsKey(flag) || i + 1 >= args.Count)
        {
            return false;
        }

        var value = args[++i];
        if (value.Length == 0 || value.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        _values[flag] = value;
        return true;
    }

    private bool TakePositional(string arg, CliSpec spec)
    {
        _positionals.Add(arg);
        return _positionals.Count <= spec.Positionals;
    }
}
