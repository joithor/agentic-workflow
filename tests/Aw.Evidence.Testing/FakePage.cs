using System.Globalization;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Lint;

namespace Aw.Evidence.Testing;

/// <summary>The state a <see cref="FakeBrowserSession"/> drives: what is visible, input values, the path, and what clicks do.</summary>
public sealed class FakePage
{
    /// <summary>Visible elements by test id.</summary>
    public ISet<string> TestIds { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Visible texts.</summary>
    public ISet<string> Texts { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>CSS selectors that match an element.</summary>
    public ISet<string> Css { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Input values by test id or CSS selector.</summary>
    public IDictionary<string, string> Inputs { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The URL path.</summary>
    public string Path { get; set; } = "/";

    /// <summary>The visible interactive elements offered to repair.</summary>
    public IList<Candidate> Candidates { get; } = [];

    /// <summary>What clicking an element does, by locator key ('testId:x', 'text:x', 'css:x', 'candidate:3').</summary>
    public IDictionary<string, Action<FakePage>> OnClick { get; } = new Dictionary<string, Action<FakePage>>(StringComparer.Ordinal);

    /// <summary>Runs after every candidate listing (to simulate the DOM shifting before the action).</summary>
    public Action<FakePage>? OnEnumerate { get; set; }

    /// <summary>Bounding boxes by locator key.</summary>
    public IDictionary<string, Box> Boxes { get; } = new Dictionary<string, Box>(StringComparer.Ordinal);

    /// <summary>The screenshot bytes returned for every capture.</summary>
#pragma warning disable CA1819 // a fake hands back the exact bytes a test sets
    public byte[] Screenshot { get; set; } = [1, 2, 3];
#pragma warning restore CA1819

    /// <summary>The lint snapshot.</summary>
    public PageSnapshot Snapshot { get; set; } = new([], new ViewportSize(1280, 800));

    /// <summary>When set, every screenshot throws it (a crashed browser, or an adapter bug that escapes its Result).</summary>
    public Exception? ScreenshotThrows { get; set; }

    /// <summary>When true, sign-in fails.</summary>
    public bool SignInFails { get; set; }

    /// <summary>The key a locator is stored under.</summary>
    /// <param name="locator">The locator.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(Locator locator) => locator switch
    {
        TestIdLocator t => $"testId:{t.TestId.Value}",
        TextLocator t => $"text:{t.Text}",
        CssLocator c => $"css:{c.Css}",
        CandidateLocator c => string.Create(CultureInfo.InvariantCulture, $"candidate:{c.Index}"),
        _ => "unknown",
    };

    /// <summary>Whether <paramref name="locator"/> finds a visible element now.</summary>
    /// <param name="locator">The locator.</param>
    /// <returns>Whether it is there.</returns>
    public bool Has(Locator locator) => locator switch
    {
        TestIdLocator t => TestIds.Contains(t.TestId.Value),
        TextLocator t => Texts.Contains(t.Text),
        CssLocator c => Css.Contains(c.Css),
        CandidateLocator c => Candidates.Any(x => x.Index == c.Index && x.TestId?.Value == c.ExpectedTestId?.Value),
        _ => false,
    };
}
