using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Theme;

/// <summary>
/// Reduced motion (UI spec §11.4, §14.3): the one place that sets the app's Motion.* durations. Reduced, every
/// duration is 0; otherwise each is its token's value from Theme/Tokens.axaml. The scaled copy is written over the
/// app's own dictionary, so DynamicResource users follow.
/// </summary>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 Unit D: the shell's start-up and Settings apply it through SettingsTheme; until the integrator wires them only the tests call it.")]
internal static class Motion
{
    /// <summary>Sets every motion duration in <paramref name="resources"/> (the app's resources, with the tokens merged in).</summary>
    /// <param name="resources">The app's resources.</param>
    /// <param name="reduce">True for reduced motion (the OS setting or the in-app switch).</param>
    internal static void Apply(IResourceDictionary resources, bool reduce) => throw new NotSupportedException();
}
