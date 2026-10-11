using System.Text.Json.Serialization;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Design;

/// <summary>A value typed into a form field before capture (never submitted). The field is a test id or a CSS selector, exactly one.</summary>
/// <param name="Value">The value to type.</param>
/// <param name="TestId">The field's data-testid.</param>
/// <param name="Css">A CSS selector for the field.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FormValue(string Value, TestId? TestId = null, string? Css = null)
{
    /// <summary>True when exactly one of test id and (non-empty) CSS selector is set.</summary>
    [JsonIgnore]
    public bool IsWellFormed => TestId is null ? !string.IsNullOrEmpty(Css) : Css is null;
}
