using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A group of the inbox as the page draws it: a heading, a count and the rows, patched in place on every re-read.</summary>
internal sealed class NeedsYouInboxGroup : ObservableObject
{
    internal NeedsYouInboxGroup(NeedsYouGroupKind kind) => Kind = kind;

    /// <summary>The group's kind.</summary>
    internal NeedsYouGroupKind Kind { get; }

    /// <summary>The group's heading.</summary>
    public string Heading => throw new NotSupportedException();

    /// <summary>How many rows the group holds, as the header says it.</summary>
    public string CountText => throw new NotSupportedException();

    /// <summary>The group's rows.</summary>
    public ObservableCollection<NeedsYouRowViewModel> Rows { get; } = [];
}
