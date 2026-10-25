namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>The words after 'scoping &lt;verb&gt;': what is not a flag, each value flag's value, and --json.</summary>
/// <param name="Positional">The arguments that are not flags, in order.</param>
/// <param name="Values">Each value flag given, with its value.</param>
/// <param name="Json">Whether --json was given.</param>
internal sealed record ScopingArguments(IReadOnlyList<string> Positional, IReadOnlyDictionary<string, string> Values, bool Json)
{
    /// <summary>Splits the arguments; null for a flag the verb does not take, or one whose value is missing or repeated.</summary>
    /// <param name="args">The arguments after the verb.</param>
    /// <param name="valueFlags">The flags that take a value.</param>
    internal static ScopingArguments? Parse(IReadOnlyList<string> args, params string[] valueFlags)
    {
        var positional = new List<string>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var json = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--json", StringComparison.Ordinal))
            {
                json = true;
            }
            else if (valueFlags.Contains(arg, StringComparer.Ordinal))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(arg, args[++i]))
                {
                    return null;
                }
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }
            else
            {
                positional.Add(arg);
            }
        }

        return new ScopingArguments(positional, values, json);
    }

    /// <summary>A value flag's value, or null when it was not given.</summary>
    internal string? Value(string flag) => Values.GetValueOrDefault(flag);
}
