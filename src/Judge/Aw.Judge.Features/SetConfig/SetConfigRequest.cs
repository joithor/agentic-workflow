namespace Aw.Judge.Features.SetConfig;

/// <summary>The SetConfig request ('judge config set &lt;question&gt; &lt;enabled|threshold&gt; &lt;value&gt;').</summary>
/// <param name="Question">The question name.</param>
/// <param name="Field">"enabled", or the threshold for anything else.</param>
/// <param name="Value">The value as typed.</param>
public sealed record SetConfigRequest(string Question, string Field, string Value);
