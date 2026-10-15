using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of the item verbs ('queue', 'items', 'item add', 'item close') into one <see cref="Parsed"/>, or the usage error.</summary>
internal static class ItemVerbArguments
{
    internal const string Usage = "queue [--json] [--no-start] | items [--state S] [--repo R] [--json] [--no-start] | item add <title> [--repo R] [--no-start] | item close <item> [--cancelled] [--version N] [--no-start]";

    internal static Result<Parsed> Parse(string[] args)
    {
        var (verb, rest) = args switch
        {
            ["queue", .. var r] => (Verb.Queue, r),
            ["items", .. var r] => (Verb.Items, r),
            ["item", "add", .. var r] => (Verb.Add, r),
            ["item", "close", .. var r] => (Verb.Close, r),
            _ => (Verb.None, args),
        };
        if (verb == Verb.None)
        {
            return SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not an item command: {Usage}");
        }

        var parsed = new Parsed(verb);
        var words = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            var taken = Flag(parsed, rest, i, words);
            if (taken.IsError)
            {
                return taken.Error;
            }

            i += taken.Value;
        }

        var shape = Shape(parsed, words);
        if (shape is not null)
        {
            return shape;
        }

        return parsed;
    }

    /// <summary>Reads the argument at <paramref name="i"/> into <paramref name="parsed"/> (or <paramref name="words"/>); the extra arguments it consumed.</summary>
    private static Result<int> Flag(Parsed parsed, string[] rest, int i, List<string> words)
    {
        var verb = parsed.Verb;
        var value = i + 1 < rest.Length ? rest[i + 1] : null;
        switch (rest[i])
        {
            case "--json" when verb is Verb.Queue or Verb.Items:
                parsed.Json = true;
                return 0;
            case "--no-start":
                parsed.NoStart = true;
                return 0;
            case "--cancelled" when verb == Verb.Close:
                parsed.Cancelled = true;
                return 0;
            case "--state" or "--repo" or "--version" when value is not null && Takes(verb, rest[i]):
                return Valued(parsed, rest[i], value);
            case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                return SindriErrors.InvalidInput.With($"'{flag}' isn't a flag of this command (or it needs a value): {Usage}");
            case var word:
                words.Add(word);
                return 0;
        }
    }

    private static bool Takes(Verb verb, string flag) => flag switch
    {
        "--state" => verb == Verb.Items,
        "--repo" => verb is Verb.Items or Verb.Add,
        _ => verb == Verb.Close,
    };

    private static Result<int> Valued(Parsed parsed, string flag, string value)
    {
        switch (flag)
        {
            case "--state":
                parsed.State = value;
                break;
            case "--repo":
                parsed.Repo = value;
                break;
            default:
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    return SindriErrors.InvalidInput.With($"--version needs a whole number, not '{value}'");
                }

                parsed.Version = version;
                break;
        }

        return 1;
    }

    /// <summary>Checks the plain words the verb took and keeps them; the error, or null when they fit.</summary>
    private static Error? Shape(Parsed parsed, List<string> words)
    {
        switch (parsed.Verb)
        {
            case Verb.Add when words.Count == 0:
                return SindriErrors.InvalidInput.With("item add needs a title: sindri item add <title> [--repo R]");
            case Verb.Close when words.Count != 1:
                return SindriErrors.InvalidInput.With("item close needs one item (an id or a tracker key): sindri item close <item> [--cancelled] [--version N]");
            case Verb.Queue or Verb.Items when words.Count > 0:
                return SindriErrors.InvalidInput.With($"'{words[0]}' isn't an argument of this command: {Usage}");
            case Verb.None or Verb.Queue or Verb.Items or Verb.Add or Verb.Close:
            default:
                parsed.Text = string.Join(' ', words);
                return null;
        }
    }

    internal enum Verb
    {
        None,
        Queue,
        Items,
        Add,
        Close,
    }

    /// <summary>The arguments of one item verb; <see cref="Text"/> is the title of 'add' or the item of 'close'.</summary>
    /// <param name="Verb">The verb.</param>
    internal sealed record Parsed(Verb Verb)
    {
        public bool Json { get; set; }

        public bool NoStart { get; set; }

        public bool Cancelled { get; set; }

        public string? State { get; set; }

        public string? Repo { get; set; }

        public long? Version { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
