namespace Aw.Evidence.Core.Lint;

/// <summary>The computed style values the lint rules read.</summary>
/// <param name="TextOverflow">CSS text-overflow.</param>
/// <param name="Overflow">CSS overflow.</param>
/// <param name="Color">CSS color as the browser computed it.</param>
public sealed record ElementStyle(string? TextOverflow, string? Overflow, string? Color);
