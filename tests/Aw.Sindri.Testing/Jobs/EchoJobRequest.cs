using Aw.Sindri.Core.Commands;
namespace Aw.Sindri.Testing.Jobs;

/// <summary>A test job (API P6): writes <paramref name="Lines"/> progress lines, waiting <paramref name="DelayMs"/> after each, honours its token and returns an <see cref="EchoReport"/>. Registered only in the test host.</summary>
/// <param name="Lines">How many lines to report.</param>
/// <param name="DelayMs">The wait after each line, in milliseconds.</param>
/// <param name="Throws">True to throw after the last line, for the runner contract's "a throw is a failed job".</param>
public sealed record EchoJobRequest(int Lines, int DelayMs, bool Throws = false) : IJob<EchoReport>;
