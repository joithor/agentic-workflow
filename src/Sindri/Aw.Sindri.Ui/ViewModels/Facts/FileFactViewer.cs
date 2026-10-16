#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A file fact: its name, size and hash, and Open.</summary>
internal sealed class FileFactViewer : ObservableObject, IFactViewer
{
    internal FileFactViewer(FactRow row) => throw new NotSupportedException();

    public FactRow Row => throw new NotSupportedException();

    /// <summary>The file's name as served.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>The size in words ("18 KB").</summary>
    public string SizeText => throw new NotSupportedException();

    /// <summary>The hash: its algorithm and the first twelve characters.</summary>
    public string HashText => throw new NotSupportedException();

    /// <summary>The path as served.</summary>
    public string Path => throw new NotSupportedException();

    /// <summary>True when a path is served, so there is something to open.</summary>
    public bool CanOpen => throw new NotSupportedException();

    /// <summary>Open's accessible name.</summary>
    public string OpenName => throw new NotSupportedException();

    /// <summary>What Open found, in words; empty before it was pressed.</summary>
    public string OpenNote => throw new NotSupportedException();

    public Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Records what Open found.</summary>
    /// <param name="opened">True when the platform opened the file.</param>
    public void ReportOpened(bool opened) => throw new NotSupportedException();
}
