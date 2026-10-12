using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Engine;

/// <summary>One record a <see cref="FakeContextSource"/> holds.</summary>
/// <param name="Ref">Its reference (the version it is cited at).</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its text.</param>
public sealed record FakeRecord(SourceRef Ref, string Title, string Text);
