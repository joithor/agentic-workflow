using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What an instance is about (UI screens §5.4, API spec §3.8): the recipe's first declared input, named by the kind of the
/// fact's VALUE. An item is its ITM- id; a link is its tracker key (else its URL); a file is its name; a text is its first line
/// (the key cut to 40 characters, the label to 80); a payload is the source's 'id' or 'number' (else the fact key) and its
/// 'title' or 'summary' (else the key). The key is also the value of the 'Subject' search attribute, so the list row and the
/// attribute can't disagree. The label of an item is its title, which only the composers' context knows: here it is the id.
/// </summary>
public static partial class SubjectRefs
{
    /// <summary>The longest subject key of a text.</summary>
    public const int KeyLength = 40;

    /// <summary>The longest subject label of a text.</summary>
    public const int LabelLength = 80;

    /// <summary>The subject of an instance of <paramref name="recipe"/>, from the fact's value when it is text, a link or a payload.</summary>
    /// <param name="recipe">The recipe and its input facts.</param>
    /// <param name="item">The item the instance is on.</param>
    /// <param name="content">The first input's stored content as text; null when it was not read (then the key is the fact key for a text, link or payload).</param>
    /// <returns>The subject, or null when the recipe declares no input.</returns>
    public static SubjectRef? Of(InterpretedRecipe recipe, ItemKey item, string? content = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.Recipe.Inputs.Count == 0)
        {
            return null;
        }

        var input = recipe.Recipe.Inputs[0];
        var kind = input.Kind ?? input.Type ?? FactTypes.Payload;
        var (key, label) = Named(recipe, input, item, content);
        return new SubjectRef(kind, key, label, Href: null);
    }

    private static (string Key, string Label) Named(InterpretedRecipe recipe, RecipeInput input, ItemKey item, string? content)
    {
        var fact = input.Key.Value;
        var type = ValueKind(input);
        if (type is FactType.Item)
        {
            return Same(item.Value);
        }

        if (type is FactType.File)
        {
            return Same(recipe.Inputs.TryGetValue(input.Key, out var file) ? file.Name : input.Label ?? Bare(fact));
        }

        return content is null
            ? (fact, recipe.Inputs.TryGetValue(input.Key, out var named) ? named.Name : input.Label ?? Bare(fact))
            : type switch
            {
                FactType.Text => Text(content),
                FactType.Link => Link(content),
                FactType.Item or FactType.File or FactType.ScopeMap or FactType.Packet or FactType.Evidence or FactType.Decision or FactType.Payload or _ => Payload(content, fact),
            };
    }

    /// <summary>Whether the first input's subject needs the stored content (a text, a link or a payload).</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>True when the workflow should read the first input's content before naming the subject.</returns>
    public static bool NeedsContent(InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.Recipe.Inputs.Count > 0 && ValueKind(recipe.Recipe.Inputs[0]) is not (FactType.Item or FactType.File);
    }

    /// <summary>A fact key without its 'fact:' prefix.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The name.</returns>
    internal static string Bare(string key) => key.StartsWith("fact:", StringComparison.Ordinal) ? key["fact:".Length..] : key;

    /// <summary>The text cut to <paramref name="length"/> characters, never between the two halves of a surrogate pair.</summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The most characters to keep.</param>
    /// <returns>The cut text.</returns>
    internal static string Cut(string text, int length) =>
        text.Length <= length ? text : text[..(char.IsHighSurrogate(text[length - 1]) ? length - 1 : length)];

    /// <summary>The kind of the fact's value: its declared type, else the subject kind the recipe names, else text.</summary>
    private static FactType ValueKind(RecipeInput input) => FactTypes.Parse(input.Type ?? input.Kind ?? "text");

    private static (string Key, string Label) Same(string value) => (value, value);

    private static (string Key, string Label) Text(string content)
    {
        var line = content.Split('\n').Select(static l => l.Trim()).FirstOrDefault(static l => l.Length > 0) ?? string.Empty;
        return (Cut(line, KeyLength), Cut(line, LabelLength));
    }

    private static (string Key, string Label) Link(string content)
    {
        var (url, title, tracker) = LinkFields(content.Trim());
        var key = tracker ?? Tracker(url) ?? url;
        return (key, title ?? key);
    }

    /// <summary>A link's fields: a JSON object with 'url' (and 'key', 'title'), or the URL itself.</summary>
    private static (string Url, string? Title, string? Tracker) LinkFields(string content)
    {
        if (!content.StartsWith('{'))
        {
            return (content, null, null);
        }

        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            return (Field(root, "url") ?? string.Empty, Field(root, "title"), Field(root, "key"));
        }
        catch (JsonException)
        {
            return (content, null, null);   // boundary: stored content that isn't JSON is the URL as written
        }
    }

    private static (string Key, string Label) Payload(string content, string fact)
    {
        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            var key = Field(root, "id") ?? Field(root, "number") ?? fact;
            return (key, Field(root, "title") ?? Field(root, "summary") ?? key);
        }
        catch (JsonException)
        {
            return (fact, fact);   // boundary: a payload that isn't JSON names nothing but its fact
        }
    }

    private static string? Field(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String when value.GetString() is { Length: > 0 } text => text,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or _ => null,
            }
            : null;

    /// <summary>The tracker key in a URL's path ('ENG-142' in '.../issue/ENG-142/title'), if there is one.</summary>
    private static string? Tracker(string url)
    {
        var match = TrackerKey.Match(url);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Z][A-Z0-9]{1,9}-\d+(?![A-Za-z0-9])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrackerKey { get; }
}
