using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One input the open run filled: its metadata and the request it was filled from (history already holds both; no text).</summary>
/// <param name="Recorded">The metadata the 'FillInput' activity returned.</param>
/// <param name="Request">What the input was filled from.</param>
internal sealed record InjectionFill(FilledInput Recorded, FillInputRequest Request);
