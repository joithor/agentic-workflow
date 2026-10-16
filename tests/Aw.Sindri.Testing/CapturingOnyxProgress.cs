using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Progress that remembers what it was told.</summary>
public sealed class CapturingOnyxProgress : IOnyxProgress
{
    /// <summary>Gets the lines reported, in order.</summary>
    public IList<string> Lines { get; } = [];

    /// <summary>Gets the number of ticks.</summary>
    public int Ticks { get; private set; }

    /// <inheritdoc />
    public void Line(string text) => Lines.Add(text);

    /// <inheritdoc />
    public void Tick() => Ticks++;
}
