namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One repo's line inside a step that counts repos: its name, its state in a word, and what is true now.</summary>
internal sealed class SetupItemViewModel
{
    internal SetupItemViewModel(string repo, string state, string? detail)
    {
        var (_, word, _) = Labels.SetupChip(state);
        Repo = repo;
        ChipText = word;
        DetailText = detail ?? string.Empty;
        Name = repo + ", " + word.ToLowerInvariant() + (DetailText.Length > 0 ? ". " + DetailText : string.Empty);
    }

    /// <summary>The repo's name.</summary>
    public string Repo { get; }

    /// <summary>The state in a word.</summary>
    public string ChipText { get; }

    /// <summary>What is true now, or empty.</summary>
    public string DetailText { get; }

    /// <summary>True when there is something to say.</summary>
    public bool HasDetail => DetailText.Length > 0;

    /// <summary>The line's accessible name: "demo, working".</summary>
    public string Name { get; }
}
