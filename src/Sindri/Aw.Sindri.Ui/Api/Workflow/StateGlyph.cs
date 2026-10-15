using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A state's progress glyph. A glyph this build doesn't know reads as <see cref="Pending"/> (API rev 8).</summary>
[JsonConverter(typeof(StateGlyphJsonConverter))]
internal enum StateGlyph
{
    /// <summary>Passed.</summary>
    Done = 1,

    /// <summary>Where the instance is.</summary>
    Active = 2,

    /// <summary>Not reached; also what an unknown glyph reads as.</summary>
    Pending = 3,
}
