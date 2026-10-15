using System.Globalization;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Culture-invariant text for the numbers and the interpolated strings a headline is built from.</summary>
internal static class Number
{
    internal static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    internal static string Text(string text) => text;
}
