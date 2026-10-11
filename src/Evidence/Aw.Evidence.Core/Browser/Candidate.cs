using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Browser;

/// <summary>A currently visible interactive element, offered to selector repair.</summary>
/// <param name="Index">Its DOM-order position among interactive elements.</param>
/// <param name="Role">Its role attribute, or its tag name.</param>
/// <param name="AccessibleName">Its aria-label, or its text.</param>
/// <param name="TestId">Its data-testid.</param>
/// <param name="Text">Its trimmed text.</param>
public sealed record Candidate(int Index, string? Role, string? AccessibleName, TestId? TestId, string? Text);
