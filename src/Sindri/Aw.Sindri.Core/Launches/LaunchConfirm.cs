using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>Why another Open should ask first.</summary>
/// <param name="Reason">The sentence the app asks.</param>
public sealed record LaunchConfirm(string Reason);
