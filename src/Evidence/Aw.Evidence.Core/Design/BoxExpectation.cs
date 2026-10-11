using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Design;

/// <summary>A geometry assertion on one element.</summary>
/// <param name="Target">The element measured.</param>
/// <param name="Expected">The expected axes.</param>
/// <param name="RelativeTo">Measure x and y from this element's top-left instead of the page's.</param>
/// <param name="Tolerance">Allowed absolute deviation per axis in pixels; null is 0.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoxExpectation(ElementSelector Target, ExpectedGeometry Expected, ElementSelector? RelativeTo = null, double? Tolerance = null);
