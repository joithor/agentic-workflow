using System.Globalization;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>Counts as progress lines print them ('1,400').</summary>
internal static class Counts
{
    public static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
