#pragma warning disable MA0041 // red-phase stub: members throw until the implementation commit
namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One repo's line inside a step that counts repos: its name, its state as a glyph and a word, and what is true now.</summary>
internal sealed class SetupItemViewModel
{
    /// <summary>The repo's name.</summary>
    public string Repo => throw new NotSupportedException();

    /// <summary>The state: glyph and word.</summary>
    public string ChipText => throw new NotSupportedException();

    /// <summary>What is true now, or empty.</summary>
    public string DetailText => throw new NotSupportedException();

    /// <summary>True when there is something to say.</summary>
    public bool HasDetail => throw new NotSupportedException();

    /// <summary>The line's accessible name: "demo, working".</summary>
    public string Name => throw new NotSupportedException();
}
