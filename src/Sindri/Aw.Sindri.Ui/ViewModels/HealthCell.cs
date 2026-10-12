namespace Aw.Sindri.Ui.ViewModels;

/// <summary>One labelled value of the health line (UI spec §4.8): a cell of Home's strip, or a row of the Health page.</summary>
/// <param name="Label">What it is ("Process", "pid · epoch").</param>
/// <param name="Value">The value in words.</param>
internal sealed record HealthCell(string Label, string Value);
