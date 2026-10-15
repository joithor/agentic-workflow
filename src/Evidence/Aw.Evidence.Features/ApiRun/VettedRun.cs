using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>A script that passed every guard before the locks: the host, and each request with its header values and golden text resolved.</summary>
/// <param name="Script">The script.</param>
/// <param name="Host">The host the requests go to; null when the script has no requests.</param>
/// <param name="Requests">The requests, in order, resolved.</param>
internal sealed record VettedRun(ApiScript Script, ResolvedHost? Host, IReadOnlyList<ResolvedRequest> Requests);
