using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>A workstream of a children group, in the order served.</summary>
internal sealed class ChildStreamViewModel
{
    internal ChildStreamViewModel(Workstream stream, Func<ChildRow, ChildRowViewModel> make)
    {
        Stream = stream.Stream;
        Label = stream.Label;
        Title = Labels.StreamTitle(stream.Stream, stream.Label);
        Rows = [.. stream.Rows.Select(make)];
    }

    /// <summary>The stream's id.</summary>
    public string Stream { get; }

    /// <summary>The stream's label.</summary>
    public string Label { get; }

    /// <summary>"ws-1 UI".</summary>
    public string Title { get; }

    /// <summary>The stream's children.</summary>
    public IReadOnlyList<ChildRowViewModel> Rows { get; }
}
