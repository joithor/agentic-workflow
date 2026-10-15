using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Culture-invariant text for the numbers and the interpolated strings a headline is built from.</summary>
internal static partial class Number
{
    internal static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    internal static string Text(string text) => text;

    /// <summary>The text with each long path cut to its file name ('…/tool'), so a headline's cap keeps the reason; the full text stays in the refusal message.</summary>
    internal static string ShortPaths(string text) =>
        PathToken.Replace(text, static match => match.Length <= 40 ? match.Value : "…/" + match.Value[(match.Value.LastIndexOf('/') + 1)..]);

    [GeneratedRegex("""[^\s'"(),]*/[^\s'"(),]+""", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex PathToken { get; }
}
