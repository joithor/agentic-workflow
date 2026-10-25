using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The ports a launch reads beside the target and the launcher, grouped to keep <see cref="AgentLaunching"/> small.</summary>
/// <param name="Bundles">Resolves the pack a Step bundles into its directory (LN-WF5).</param>
/// <param name="Contents">Reads the stored 'fact:lensQueue' whose head lens a 'lenses' bundle holds, the stored 'fact:workspace', and the facts a Step declares as inputs.</param>
/// <param name="Instructions">What each Step that has instructions is told to do.</param>
/// <param name="Scrubber">Removes secrets from the facts written into a Step's run directory.</param>
internal sealed record AgentLaunchInputs(IPackBundles Bundles, IFactContents Contents, IEnumerable<IAgentStepInstructions> Instructions, ITextScrubber Scrubber);
