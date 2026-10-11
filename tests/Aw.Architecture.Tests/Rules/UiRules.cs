namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The UI package's own rules (.agents/rules/csharp.md "The UI package"), over the unshaped UI assemblies: view models
/// carry no Avalonia types (so they test in the unit lane), and a platform head holds no views or view models.
/// </summary>
public static class UiRules
{
    private static readonly string[] _viewTypes = ["Avalonia.Controls.Window", "Avalonia.Controls.UserControl", "System.ComponentModel.INotifyPropertyChanged"];

    /// <summary>The rule id.</summary>
    public const string Id = "ARCH-UI-001";

    /// <summary>Checks the edges for an Avalonia dependency from a view model and a view or view model in a head.</summary>
    /// <param name="edges">Every dependency edge among the registered assemblies' types.</param>
    /// <returns>One violation per offending type.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) =>
        edges
            .Where(e => IsViewModel(e) && e.Target.FullName.StartsWith("Avalonia.", StringComparison.Ordinal))
            .Select(e => new Violation(Id, $"{TypeNames.Qualified(e.Origin)} uses {e.Target.FullName}",
                "keep view models plain C# (INotifyPropertyChanged, BCL collections); Avalonia types belong in Views"))
            .Concat(edges
                .Where(e => TypeNames.NamespaceOf(e.Origin).StartsWith("Aw.Sindri.Ui.Desktop", StringComparison.Ordinal)
                    && _viewTypes.Contains(e.Target.FullName, StringComparer.Ordinal))
                .Select(e => new Violation(Id, $"{TypeNames.Qualified(e.Origin)} uses {e.Target.FullName}",
                    "a head holds only Main, composition and adapters; put views and view models in Aw.Sindri.Ui")))
            .Distinct();

    private static bool IsViewModel(Edge e) => TypeNames.NamespaceOf(e.Origin).StartsWith("Aw.Sindri.Ui.ViewModels", StringComparison.Ordinal);
}
