using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>The registry of credential sources, keyed by id (sandbox spec SB18). Adapters are registered explicitly in the DI registration.</summary>
public sealed class ModelCredentialSources
{
    /// <summary>Creates the registry.</summary>
    /// <param name="sources">The registered adapters; two with one id is a programming error.</param>
    public ModelCredentialSources(IEnumerable<IModelCredentialSource> sources)
    {
        throw new NotSupportedException();
    }

    /// <summary>Whether an adapter is registered for <paramref name="id"/>.</summary>
    /// <param name="id">The source id.</param>
    /// <returns>True when it is.</returns>
    public bool Has(ModelLoginSource id) => throw new NotSupportedException();

    /// <summary>Checks the login cheaply: ready, or SND-LAUNCH-017 with the source id. Never tries another source.</summary>
    /// <param name="id">The source.</param>
    /// <param name="target">The agent.</param>
    /// <returns>The status, or the -017 error.</returns>
    public Result<LoginStatus> Check(ModelLoginSource id, AgentId target) => throw new NotSupportedException();

    /// <summary>Prepares the login for one launch from exactly the chosen source.</summary>
    /// <param name="id">The source.</param>
    /// <param name="target">The agent.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The prepared login, or SND-LAUNCH-017 with the source id.</returns>
    public Task<Result<PreparedLogin>> Prepare(ModelLoginSource id, AgentId target, LaunchId launch, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Releases a prepared login through its source.</summary>
    /// <param name="prepared">The prepared login.</param>
    public void Release(PreparedLogin prepared) => throw new NotSupportedException();
}
