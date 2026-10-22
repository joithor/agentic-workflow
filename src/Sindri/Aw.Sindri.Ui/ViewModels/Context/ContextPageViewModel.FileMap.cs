using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map seam (U9d owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The file map.</summary>
    public FileMapViewModel FileMap { get; private set; }

    /// <summary>The code lens' callers and callees panels (S-9).</summary>
    internal CodeLensViewModel Code { get; } = new();

    [MemberNotNull(nameof(FileMap))]
    private void InitFileMap() => FileMap = new FileMapViewModel(Ports);
}
