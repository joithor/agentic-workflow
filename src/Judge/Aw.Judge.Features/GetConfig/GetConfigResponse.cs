using Aw.Judge.Core.Config;

namespace Aw.Judge.Features.GetConfig;

/// <summary>The GetConfig response.</summary>
/// <param name="Config">The effective config.</param>
public sealed record GetConfigResponse(JudgeConfig Config);
