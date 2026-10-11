using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Lint;

/// <summary>One interactive or test-id element as captured from the page.</summary>
/// <param name="Selector">A short selector naming it ('button#save').</param>
/// <param name="Rect">Its bounding box.</param>
/// <param name="Text">Its trimmed text.</param>
/// <param name="Style">Its computed style.</param>
/// <param name="HasHoverState">Whether it has a hover style.</param>
/// <param name="HasFocusState">Whether it has a focus style.</param>
public sealed record PageElement(string Selector, Box Rect, string Text, ElementStyle Style, bool HasHoverState, bool HasFocusState);
