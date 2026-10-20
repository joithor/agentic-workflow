namespace Aw.Sindri.Core.Profile;

/// <summary>The line's profile values with their defaults (line spec Appendix D): what a key left out means.</summary>
public static class LineProfile
{
    /// <summary>workflow.line.parallel: 2.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int Parallel(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.line.maxOpenPrs: 4.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int MaxOpenPrs(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.line.maxStack: 4.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int MaxStack(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.plan.maxPhaseSize: M.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static string MaxPhaseSize(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.prove.holdoutMin: 2.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int HoldoutMin(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.steps.longMax: 2h.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static string LongMax(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.docs.globs: docs/** and *.md.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The globs.</returns>
    public static IReadOnlyList<string> DocsGlobs(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>workflow.ship.draft: false.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static bool Draft(SindriProfile profile) => throw new NotSupportedException();

    /// <summary>The sizes file aw-packets reads for PKT-SIZE-001 (LN-PKT3): the five size classes from workflow.plan.sizes, a size or a field left out taking its default (XS 3 files and 2 items, S 8 and 4, M 20 and 8, spec Q5).</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The sizes file's JSON text.</returns>
    public static string SizesFile(SindriProfile profile) => throw new NotSupportedException();
}
