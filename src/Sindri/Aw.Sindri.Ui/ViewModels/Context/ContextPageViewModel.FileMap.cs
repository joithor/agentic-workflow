using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map seam (U9d owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The file map.</summary>
    public FileMapViewModel FileMap { get; private set; }

    [MemberNotNull(nameof(FileMap))]
    private void InitFileMap() => FileMap = new FileMapViewModel(Ports);
}
