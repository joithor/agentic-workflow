using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>What a brief source reads with: the approved settings, the mapped repos and the call's scrubbers.</summary>
/// <param name="Settings">The checked 'context:' settings.</param>
/// <param name="Repos">The repos in scope.</param>
/// <param name="Scrubbers">This call's scrubbers (the brief is cleaned with them before anything reads it).</param>
public sealed record BriefScope(ContextSettings Settings, IReadOnlyList<ContextRepo> Repos, Scrubbers Scrubbers);
