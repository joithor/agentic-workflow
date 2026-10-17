using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A scripted test job (API P6 unit C): 'fail' returns an error, 'throw' throws, 'big' returns a 20 KB result, 'secret' reports a token, 'job' returns the job id it sees, 'hold*' waits for its token, 'stubborn' ignores its token until the latch is released, 'own-cancel' then throws a cancellation of its own.</summary>
/// <param name="Mode">What the work does; it is also the job's key.</param>
public sealed record ProbeJobRequest(string Mode) : IJob<ProbeReport>;
