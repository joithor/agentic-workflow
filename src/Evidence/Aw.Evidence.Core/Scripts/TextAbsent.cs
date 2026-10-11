using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>No visible element shows <paramref name="Text"/>.</summary>
/// <param name="Text">The text (not empty).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextAbsent(string Text) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"text \"{Text}\" absent";

    /// <inheritdoc/>
    public override string? Problem() => Text.Length == 0 ? "text-absent needs a text" : null;
}
