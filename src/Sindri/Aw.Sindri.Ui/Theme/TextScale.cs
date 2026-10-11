using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Theme;

/// <summary>
/// The user's text size (UI spec §14.2, Settings): scales the type ramp in Theme/Tokens.axaml by 0.9–1.3. The base sizes
/// stay in the token dictionary; the scaled ones are written over them, so the typography styles' DynamicResource
/// setters follow.
/// </summary>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 Unit D's Settings and the shell's start-up apply the stored scale; until they land only the theme tests call it.")]
internal static class TextScale
{
    /// <summary>The smallest scale.</summary>
    internal const double Min = 0.9;

    /// <summary>The largest scale.</summary>
    internal const double Max = 1.3;

    /// <summary>Scales the ramp in <paramref name="resources"/> (the app's resources, with the tokens merged in).</summary>
    /// <param name="resources">The app's resources.</param>
    /// <param name="scale">The scale; clamped to <see cref="Min"/>–<see cref="Max"/>.</param>
    internal static void Apply(IResourceDictionary resources, double scale) => throw new NotSupportedException();
}
