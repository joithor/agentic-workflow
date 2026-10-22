using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A row's timeout: what it does and the instant it fires (the served seconds counted from when the list was read).</summary>
/// <param name="Kind">Parks the run or escalates.</param>
/// <param name="Due">When it fires.</param>
internal sealed record RowTimeout(TimeoutKind Kind, DateTimeOffset Due);
