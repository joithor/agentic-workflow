using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Browser;

/// <summary>How the browser finds one element.</summary>
public abstract record Locator
{
    /// <summary>The element with this data-testid.</summary>
    /// <param name="testId">The test id.</param>
    /// <returns>The locator.</returns>
    public static Locator ByTestId(TestId testId) => new TestIdLocator(testId);

    /// <summary>The first visible element showing this text (layouts often render a hidden copy too).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The locator.</returns>
    public static Locator ByText(string text) => new TextLocator(text);

    /// <summary>The first element matching a CSS selector.</summary>
    /// <param name="css">The selector.</param>
    /// <returns>The locator.</returns>
    public static Locator ByCss(string css) => new CssLocator(css);

    /// <summary>The locator for a manifest selector.</summary>
    /// <param name="selector">A well-formed selector.</param>
    /// <returns>The locator.</returns>
    public static Locator For(ElementSelector selector) =>
        selector.TestId is { } id ? ByTestId(id) : ByText(selector.Text ?? string.Empty);

    /// <summary>The locator for a form value's field.</summary>
    /// <param name="value">A well-formed form value.</param>
    /// <returns>The locator.</returns>
    public static Locator For(FormValue value) =>
        value.TestId is { } id ? ByTestId(id) : ByCss(value.Css ?? string.Empty);
}
