using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>Resolves which model login source a launch uses (sandbox spec SB18): repo, then target, then profile; a Step may ask for a stricter one, never a looser one.</summary>
public static class LoginSourceResolver
{
    /// <summary>Resolves the source.</summary>
    /// <param name="settings">The profile's sandbox section, or null.</param>
    /// <param name="target">The agent.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="stepAsk">The source the Step's policy asks for, or null.</param>
    /// <returns>The source in force.</returns>
    public static ModelLoginSource Resolve(SandboxSettings? settings, AgentId target, RepoName repo, ModelLoginSource? stepAsk) => throw new NotSupportedException();
}
