using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's reads of the API (<see cref="ISindriApi"/>, continued): the context code lens.</summary>
internal partial interface ISindriApi
{
    /// <summary>QUERY /sindri/context/code: the callers and callees the code index gave for the files around the anchors (the JSON body).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The anchors and the flags.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The code lens' answer, the API's refusal (SND-CONTEXT-003, -006), or SND-UI-002/003.</returns>
    public Task<Result<CodeView>> Code(Uri api, CodeQuery query, CancellationToken cancellationToken);
}
