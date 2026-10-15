using System.Globalization;

namespace Aw.Meta.Host.Commands;

/// <summary>Reads a pull request number given as "190" or "#190".</summary>
internal static class PrArgument
{
    internal static bool TryParse(string text, out int pr) =>
        int.TryParse(text.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out pr) && pr > 0;

    internal static int Usage(CommandContext context, CommandEntry entry) =>
        CommandOutput.Usage(context.Io, "the pull request must be a number such as 190 or #190", entry.Spec.Usage);
}
