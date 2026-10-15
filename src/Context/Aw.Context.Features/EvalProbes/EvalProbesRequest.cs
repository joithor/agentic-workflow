using Aw.Context.Core.Eval;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Features.EvalProbes;

/// <summary>A probe run.</summary>
/// <param name="File">The probe file's path, or null for the default.</param>
/// <param name="Ask">Answers one probe's question.</param>
internal sealed record EvalProbesRequest(string? File, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> Ask);
