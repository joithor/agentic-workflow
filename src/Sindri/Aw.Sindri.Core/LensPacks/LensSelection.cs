namespace Aw.Sindri.Core.LensPacks;

/// <summary>
/// Which of a pack's lenses a round queues and which it records as not applicable (line spec §5.2: 'a lens not selected is recorded not-applicable with the reason,
/// never silently skipped'). Pure.
/// </summary>
/// <param name="Queued">The ids of the lenses that apply, in pack order.</param>
/// <param name="NotApplicable">The lenses that do not, each with why, in pack order.</param>
public sealed record LensSelection(IReadOnlyList<LensId> Queued, IReadOnlyList<(LensId Lens, string Reason)> NotApplicable)
{
    /// <summary>Selects the pack's lenses for a subject.</summary>
    /// <param name="pack">The pack.</param>
    /// <param name="surfaceKinds">The subject's surface kinds (design); null when the subject does not say, so a 'surfaceKinds' lens applies.</param>
    /// <param name="changedPaths">The changed paths (review); null when the change does not say, so a 'paths' lens applies.</param>
    /// <returns>The selection.</returns>
    public static LensSelection Of(LensPack pack, IReadOnlyCollection<string>? surfaceKinds, IReadOnlyList<string>? changedPaths)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var queued = new List<LensId>();
        var skipped = new List<(LensId, string)>();
        foreach (var lens in pack.Lenses)
        {
            if (Reason(lens.When, surfaceKinds, changedPaths) is { } reason)
            {
                skipped.Add((lens.Id, reason));
            }
            else
            {
                queued.Add(lens.Id);
            }
        }

        return new LensSelection(queued, skipped);
    }

    // Null when the lens applies; else why it does not.
    private static string? Reason(LensWhen when, IReadOnlyCollection<string>? surfaceKinds, IReadOnlyList<string>? changedPaths)
    {
        if (when.IsAlways || KindApplies(when, surfaceKinds) || PathApplies(when, changedPaths))
        {
            return null;
        }

        var kinds = $"no surface of kind {string.Join(", ", when.SurfaceKinds)} in the scope";
        var paths = $"no changed path matches {string.Join(", ", when.Paths)}";
        return when.SurfaceKinds.Count == 0 ? paths : when.Paths.Count == 0 ? kinds : $"{kinds} and {paths}";
    }

    private static bool KindApplies(LensWhen when, IReadOnlyCollection<string>? surfaceKinds) =>
        when.SurfaceKinds.Count > 0 && (surfaceKinds is null || surfaceKinds.Any(k => when.SurfaceKinds.Contains(k, StringComparer.OrdinalIgnoreCase)));

    private static bool PathApplies(LensWhen when, IReadOnlyList<string>? changedPaths) =>
        when.Paths.Count > 0 && (changedPaths is null || changedPaths.Any(p => when.Paths.Any(g => PathGlob.Matches(g, p))));
}
