namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>One input's fill: the metadata Temporal records and the texts that stay in memory.</summary>
/// <param name="Metadata">What the activity returns.</param>
/// <param name="Texts">What it filled.</param>
public sealed record FilledOutput(FilledInput Metadata, FilledTexts Texts);
