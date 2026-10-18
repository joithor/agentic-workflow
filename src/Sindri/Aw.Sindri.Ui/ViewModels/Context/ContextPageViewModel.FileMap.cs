namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map seam (U9d owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The file map.</summary>
    public FileMapViewModel FileMap { get; } = new();
}
