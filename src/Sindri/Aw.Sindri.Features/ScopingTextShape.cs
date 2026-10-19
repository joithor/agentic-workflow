using System.Text.Json.Nodes;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// The context engine's text shape for the scoping Steps (scoping spec §5.1, C1): the anchors it suggests for a piece of text, as the answer the desk
/// helper takes ('contextQuery'), read in process under the service scope cut to the instance's repo, as the Step inputs read their views.
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
internal sealed class ScopingTextShape(IContextEngine engine, ContextInputScopes scopes) : IScopingTextShape
{
    /// <summary>The longest question the engine takes (<see cref="QueryRules.MaxTextLength"/>); longer text is cut to its start.</summary>
    internal const int MaxText = QueryRules.MaxTextLength;

    /// <inheritdoc/>
    public async ValueTask<Result<string>> Anchors(RepoName? repo, string text, CancellationToken cancellationToken)
    {
        var scope = await scopes.For(repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var asked = await engine.Query(new ContextQuery(Cut(text)), scope.Value, cancellationToken).ConfigureAwait(false);
        return asked.IsError ? asked.Error : new JsonObject { ["suggestedAnchors"] = ScopeJson.ToArray(asked.Value.SuggestedAnchors) }.ToJsonString();
    }

    /// <summary>The first <see cref="MaxText"/> characters, never ending inside a surrogate pair.</summary>
    internal static string Cut(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= MaxText ? text : text[..(char.IsHighSurrogate(text[MaxText - 1]) ? MaxText - 1 : MaxText)];
    }
}
