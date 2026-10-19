
namespace Aw.Sindri.Features.Scoping;

/// <summary>Run the scoping rules over a set of facts (scoping spec §8.2; 'sindri scoping check').</summary>
/// <param name="Facts">Each fact's JSON text by key without the 'fact:' prefix ('scopeMap', 'acceptance', 'coverage', 'lenses', 'brief', 'answers', 'answerLog', 'challenge', 'openQuestions', 'questions', 'decision', 'anchors', 'runs', 'rounds'); a missing key makes the rules that need it not-run.</param>
/// <param name="Stage">The stage to run.</param>
public sealed record ScopingCheckRequest(IReadOnlyDictionary<string, string> Facts, ScopeStage Stage);
