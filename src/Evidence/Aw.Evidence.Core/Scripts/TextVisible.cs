using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>Some visible element shows <paramref name="Text"/>.</summary>
/// <param name="Text">The text (not empty).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextVisible(string Text) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"text \"{Text}\" visible";

    /// <inheritdoc/>
    public override string? Problem() => Text.Length == 0 ? "text-visible needs a text" : null;
}
