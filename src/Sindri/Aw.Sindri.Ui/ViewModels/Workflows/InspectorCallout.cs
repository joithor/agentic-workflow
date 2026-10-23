namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One callout of the run drawer.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Class">The style class that colours it (the status badge's).</param>
/// <param name="Icon">The Icons.axaml name of its glyph.</param>
/// <param name="Title">The title line.</param>
/// <param name="Body">The words under the title; empty for none.</param>
internal sealed record InspectorCallout(InspectorCalloutKind Kind, string Class, string Icon, string Title, string Body);
