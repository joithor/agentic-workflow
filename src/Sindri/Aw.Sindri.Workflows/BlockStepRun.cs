using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One run of a block-runtime Step (DT spec §8.2): what the 'RunBlockStep' activity is given.</summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Token">The run token '{entry}.{n}'.</param>
/// <param name="Step">The Step's block id.</param>
/// <param name="Arguments">Its 'with:' arguments, as the recipe wrote them.</param>
/// <param name="Facts">The instance's facts as stored references; the activity reads the content the Step names, so no fact content enters the workflow's history.</param>
/// <param name="Repo">The item's repo name, for the per-repo profile keys, or null.</param>
/// <param name="RepoUnreadable">'fact:item' could not be read, so the repo is unknown (not absent): a Step that resolves per-repo keys fails instead of using the box's.</param>
/// <param name="Fills">The inputs the run filled (metadata and the requests, never text): the activity gets their sections and typed results for the Step from memory, or by running them again and checking the recorded digests; null without inputs.</param>
/// <param name="LoopCaps">The last round each of the recipe's loops allows now (a granted round counts), by loop name, for a Step that reads a loop's cap; null reads as none.</param>
/// <param name="HeavyHeld">The instance holds a heavy-job slot for this run (LN-WF3): the Step's context says so, and the Step takes no second slot.</param>
internal sealed record BlockStepRun(string Address, string State, string Token, BlockId Step, IReadOnlyDictionary<string, WithValue> Arguments, IReadOnlyDictionary<FactKey, FactValue> Facts, string? Repo = null, bool RepoUnreadable = false, IReadOnlyList<InjectionFill>? Fills = null, bool HeavyHeld = false, IReadOnlyDictionary<string, int>? LoopCaps = null);
