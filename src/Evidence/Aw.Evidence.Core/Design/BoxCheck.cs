namespace Aw.Evidence.Core.Design;

/// <summary>One geometry assertion's result.</summary>
/// <param name="Target">The measured element's key.</param>
/// <param name="RelativeTo">The origin element's key, when relative.</param>
/// <param name="Expected">The expected axes.</param>
/// <param name="Actual">What was measured; null when an element was missing.</param>
/// <param name="Tolerance">The allowed deviation.</param>
/// <param name="Pass">Whether every expected axis is within tolerance.</param>
/// <param name="Failure">Why it failed, for people.</param>
public sealed record BoxCheck(
    string Target, string? RelativeTo, ExpectedGeometry Expected, MeasuredGeometry? Actual, double Tolerance, bool Pass, string? Failure);
