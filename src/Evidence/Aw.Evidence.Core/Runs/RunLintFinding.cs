using Aw.Evidence.Core.Lint;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Runs;

/// <summary>A lint finding in one viewport.</summary>
/// <param name="Viewport">The viewport.</param>
/// <param name="Rule">The rule.</param>
/// <param name="Selector">The element or pair.</param>
/// <param name="Detail">What was measured.</param>
public sealed record RunLintFinding(ViewportKind Viewport, LintRule Rule, string Selector, string Detail);
