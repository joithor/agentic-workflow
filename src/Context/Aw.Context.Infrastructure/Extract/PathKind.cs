namespace Aw.Context.Infrastructure.Extract;

/// <summary>What a path is, following links.</summary>
internal enum PathKind
{
    /// <summary>Nothing there.</summary>
    Missing,

    /// <summary>A directory.</summary>
    Directory,

    /// <summary>A file.</summary>
    File,
}
