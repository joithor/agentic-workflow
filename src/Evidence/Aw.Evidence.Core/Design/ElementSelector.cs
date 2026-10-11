using System.Text.Json.Serialization;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Design;

/// <summary>Finds one element: by data-testid, or by visible text (the first visible match). Exactly one is set.</summary>
/// <param name="TestId">The element's data-testid.</param>
/// <param name="Text">Text the element shows.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ElementSelector(TestId? TestId = null, string? Text = null)
{
    /// <summary>A selector by test id.</summary>
    /// <param name="testId">The test id.</param>
    /// <returns>The selector.</returns>
    public static ElementSelector ByTestId(TestId testId) => new(testId, Text: null);

    /// <summary>A selector by visible text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The selector.</returns>
    public static ElementSelector ByText(string text) => new(TestId: null, text);

    /// <summary>A stable key: 'testId:&lt;id&gt;' or 'text:&lt;text&gt;'.</summary>
    [JsonIgnore]
    public string Key => TestId is { } id ? $"testId:{id.Value}" : $"text:{Text}";

    /// <summary>True when exactly one of test id and (non-empty) text is set.</summary>
    [JsonIgnore]
    public bool IsWellFormed => TestId is null ? !string.IsNullOrEmpty(Text) : Text is null;
}
