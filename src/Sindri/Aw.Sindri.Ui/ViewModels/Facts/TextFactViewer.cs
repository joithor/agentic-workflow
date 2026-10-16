using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A text fact: one line and a Copy. The line is the fact's body once read, the row's summary until then.</summary>
internal sealed class TextFactViewer : ObservableObject, IFactViewer
{
    private readonly FactViewerContext _context;

    internal TextFactViewer(FactRow row, FactViewerContext context)
    {
        (Row, _context) = (row, context);
        Line = FactText.Scrub(row.Summary);
        Copying = new CopyAction(context.Clipboard, context.Clock, row.Label);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The line.</summary>
    public string Line { get; private set { Set(ref field, value); OnChanged(nameof(CopyName)); } }

    /// <summary>The Copy control's state.</summary>
    public CopyAction Copying { get; }

    /// <summary>Copy's accessible name: it carries the line.</summary>
    public string CopyName => Labels.CopyLineName(Line);

    /// <summary>Reads the fact's body.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the line shows the body.</returns>
    public async Task Load(CancellationToken cancellationToken)
    {
        if (_context.ApiUrl() is not { } url)
        {
            return;
        }

        var answer = await _context.Api.InstanceFact(url, _context.InstanceId, Row.Key, cancellationToken).ConfigureAwait(true);
        if (answer.IsSuccess && answer.Value.Body.ValueKind == JsonValueKind.String && answer.Value.Body.GetString() is { } body)
        {
            Line = FactText.Scrub(body);
        }
    }

    /// <summary>Copies the line.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task Copy(CancellationToken cancellationToken) => Copying.Run(Line, cancellationToken);
}
