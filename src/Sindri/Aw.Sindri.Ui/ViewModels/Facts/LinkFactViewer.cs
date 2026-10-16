#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A link fact: its host and title, and Open, which hands an http or https link to the platform.</summary>
internal sealed class LinkFactViewer : ObservableObject, IFactViewer
{
    internal LinkFactViewer(FactRow row) => throw new NotSupportedException();

    public FactRow Row => throw new NotSupportedException();

    /// <summary>The link's host as served.</summary>
    public string Host => throw new NotSupportedException();

    /// <summary>The link's title as served.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The link as served.</summary>
    public string Url => throw new NotSupportedException();

    /// <summary>True for an http or https link; no other scheme is opened.</summary>
    public bool CanOpen => throw new NotSupportedException();

    /// <summary>Open's words ("Open in linear.app ↗").</summary>
    public string OpenText => throw new NotSupportedException();

    /// <summary>Open's accessible name.</summary>
    public string OpenName => throw new NotSupportedException();

    public Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();
}
