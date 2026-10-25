namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One plain-words line of what the profile allows, as the API words it.</summary>
internal sealed class SetupPreviewLineViewModel
{
    internal SetupPreviewLineViewModel(string kind, string text)
    {
        (Kind, Text) = (kind, text);
    }

    /// <summary>repo, service, allows or limit.</summary>
    public string Kind { get; }

    /// <summary>The sentence.</summary>
    public string Text { get; }
}
