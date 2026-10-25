using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>A pack a <see cref="LensPacksContract"/> run has available: its id, the loop it serves and how many lenses it holds (lens ids 'lens-1', 'lens-2', …, all 'always').</summary>
/// <param name="Id">The pack id.</param>
/// <param name="Applies">The loop.</param>
/// <param name="Lenses">The number of lenses.</param>
public sealed record PackSpec(string Id, LensPackKind Applies, int Lenses)
{
    /// <summary>The lens ids, in pack order.</summary>
    /// <returns>The ids.</returns>
    public IReadOnlyList<string> LensIds() => [.. Enumerable.Range(1, Lenses).Select(static n => "lens-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture))];
}
