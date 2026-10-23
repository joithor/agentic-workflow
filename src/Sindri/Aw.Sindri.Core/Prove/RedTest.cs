namespace Aw.Sindri.Core.Prove;

/// <summary>One red test.</summary>
/// <param name="Id">The test id.</param>
/// <param name="Path">The file it is in; null for the short form.</param>
internal sealed record RedTest(string Id, string? Path);
