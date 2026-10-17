using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>Which map a read starts from: the current version, the newest draft, or a retained version by number (scoping spec C10).</summary>
/// <param name="Draft">True for the newest draft.</param>
/// <param name="Version">The version asked for, or null.</param>
public readonly record struct MapRead(bool Draft, MapVersionRef? Version)
{
    /// <summary>The current version.</summary>
    public static MapRead Current => new(Draft: false, Version: null);

    /// <summary>The newest draft.</summary>
    public static MapRead NewestDraft => new(Draft: true, Version: null);

    /// <summary>Reads what a caller asked: draft together with a version is two shapes.</summary>
    /// <param name="draft">The draft flag.</param>
    /// <param name="mapVersion">The version text, or null.</param>
    /// <returns>The read, or SND-CONTEXT-006.</returns>
    public static Result<MapRead> Of(bool draft, string? mapVersion) => throw new NotSupportedException();
}
