using System.Globalization;

namespace Aw.Architecture.Tests.Fixtures.Good.Features.ListItems;

// Environment.NewLine and ProcessorCount are pure: ARCH-PORT-001 flags only environment reads and writes.
internal static class ListItemsText
{
    public static string Lines(string first, string second) => string.Create(CultureInfo.InvariantCulture, $"{first}{Environment.NewLine}{second}{Environment.ProcessorCount}");
}
