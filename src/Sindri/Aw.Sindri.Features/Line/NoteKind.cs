namespace Aw.Sindri.Features.Line;

/// <summary>What a reserved revision question id means at the after-design stop.</summary>
internal enum NoteKind
{
    /// <summary>An ordinary question.</summary>
    None = 0,

    /// <summary>Revise the design.</summary>
    Design = 1,

    /// <summary>Revise the plan.</summary>
    Plan = 2,
}
