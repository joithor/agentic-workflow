namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>The JavaScript the browser adapter evaluates in the page (the long ones are embedded Browser/Scripts/*.js files).</summary>
internal static class PageScripts
{
    /// <summary>The elements selector repair may pick from, in DOM order (CandidateLocator indexes into this list).</summary>
    internal const string Interactive = "button, a, input, [role], [data-testid]";

    /// <summary>Waits for web fonts.</summary>
    internal const string FontsReady = "() => document.fonts.ready.then(() => true)";

    /// <summary>Blurs the focused element.</summary>
    internal const string Blur = "() => { document.activeElement?.blur?.(); return true; }";

    /// <summary>True when the point holds something interactive (clicking there would change the page).</summary>
    internal const string ControlAt =
        "({ x, y }) => !!document.elementFromPoint(x, y)?.closest('button, a, input, select, textarea, label, summary, [role], [onclick], [tabindex]')";

    /// <summary>Scrolls the window and every scrolled element back to the top.</summary>
    internal const string ResetScroll =
        "() => { window.scrollTo(0, 0); document.querySelectorAll('*').forEach((e) => { if (e.scrollTop > 0) e.scrollTop = 0; }); return true; }";

    /// <summary>Visible interactive elements (Browser/Scripts/visible-candidates.js); its argument is <see cref="Interactive"/>.</summary>
    internal static string VisibleCandidates { get; } = Embedded("visible-candidates.js");

    /// <summary>The lint snapshot (Browser/Scripts/lint-snapshot.js).</summary>
    internal static string LintSnapshot { get; } = Embedded("lint-snapshot.js");

    private static string Embedded(string name)
    {
        using var stream = typeof(PageScripts).Assembly.GetManifestResourceStream("Aw.Evidence.Infrastructure.Browser.Scripts." + name)
            ?? throw new InvalidOperationException($"the embedded page script {name} is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
