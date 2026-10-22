using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.GetApproveAllPlan;

/// <summary>What one hold would approve (setup-once spec §7.2, GET /workflow/recipes/approve-all; query): SND-SETUP-007 before a profile is approved.</summary>
public sealed record GetApproveAllPlanRequest() : IQuery<ApproveAllPlan>;
