namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The UI package's own rules (.agents/rules/csharp.md "The UI package"), over the unshaped UI assemblies: view models
/// carry no Avalonia types (so they test in the unit lane), and a platform head holds no views or view models.
/// </summary>
public static class UiRules
{
    // ArchUnitNET sees a type's direct base, so the common view bases are listed; and a view model's interface.
    private static readonly string[] _viewTypes =
    [
        "Avalonia.Controls.Control", "Avalonia.Controls.Primitives.TemplatedControl", "Avalonia.Controls.ContentControl",
        "Avalonia.Controls.UserControl", "Avalonia.Controls.Window", "Avalonia.Controls.TopLevel", "Avalonia.Application",
        "System.ComponentModel.INotifyPropertyChanged",
    ];

    /// <summary>The rule id.</summary>
    public const string Id = "ARCH-UI-001";

    /// <summary>Checks the edges for an Avalonia dependency from a view model and a view or view model in a head.</summary>
    /// <param name="edges">Every dependency edge among the registered assemblies' types.</param>
    /// <returns>One violation per offending type.</returns>
    /// <param name="root">The UI package root (its view models are root.ViewModels, its desktop head root.Desktop).</param>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges, string root = "Aw.Sindri.Ui") =>
        edges
            .Where(e => In(e, root + ".ViewModels") && e.Target.FullName.StartsWith("Avalonia.", StringComparison.Ordinal))
            .Select(e => new Violation(Id, $"{TypeNames.Qualified(e.Origin)} uses {e.Target.FullName}",
                "keep view models plain C# (INotifyPropertyChanged, BCL collections); Avalonia types belong in Views"))
            .Concat(edges
                .Where(e => In(e, root + ".Desktop")
                    && _viewTypes.Contains(e.Target.FullName, StringComparer.Ordinal))
                .Select(e => new Violation(Id, $"{TypeNames.Qualified(e.Origin)} uses {e.Target.FullName}",
                    "a head holds only Main, composition and adapters; put views and view models in Aw.Sindri.Ui")))
            .Distinct();

    private static bool In(Edge e, string ns) =>
        TypeNames.NamespaceOf(e.Origin) == ns || TypeNames.NamespaceOf(e.Origin).StartsWith(ns + ".", StringComparison.Ordinal);
}
