using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

internal sealed partial class CompleteStepService
{
    private static readonly FactKey _findings = FactKey.From("fact:findings");

    /// <summary>The finding keys of the report's 'fact:findings' output (engine P8 D10), computed from the stored bytes; null when it reports none or not in the keyed shape.</summary>
    private static IReadOnlyList<string>? FindingKeysOf(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs) =>
        outputs.TryGetValue(_findings, out var findings) ? FindingKeys.Of(findings.Span) : null;
}
