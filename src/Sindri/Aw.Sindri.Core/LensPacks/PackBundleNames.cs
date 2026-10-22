namespace Aw.Sindri.Core.LensPacks;

/// <summary>The values of an agent Step's 'bundle' parameter (line spec §5.1, §5.2, LN-WF5) and which Steps take which.</summary>
public static class PackBundleNames
{
    /// <summary>The style pack, copied into the Step directory as 'style/'.</summary>
    public const string Style = "style";

    /// <summary>The lens pack, copied into the Step directory as 'lens/'.</summary>
    public const string Lenses = "lenses";

    /// <summary>What is wrong with a Step's 'bundle' value, or null when it is fine.</summary>
    /// <param name="block">The Step block's id.</param>
    /// <param name="bundle">The value the recipe gives.</param>
    /// <returns>The problem; null when the Step may take that bundle.</returns>
    public static string? Problem(string block, string bundle)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(bundle);
        if (DirectoryOf(bundle) is null)
        {
            return $"takes '{Style}' or '{Lenses}', not '{bundle}'";
        }

        var wanted = block.StartsWith("step.lens.", StringComparison.Ordinal) ? Lenses : Style;
        return string.Equals(bundle, wanted, StringComparison.Ordinal) ? null : $"can only bundle '{wanted}', not '{bundle}'";
    }

    /// <summary>The folder a bundle's files go in inside the Step directory.</summary>
    /// <param name="bundle">'style' or 'lenses'.</param>
    /// <returns>'style' or 'lens'; null for any other value.</returns>
    public static string? DirectoryOf(string bundle) => bundle switch
    {
        Style => "style",
        Lenses => "lens",
        _ => null,
    };
}
