namespace Aw.Sindri.Features.OnyxUp;

/// <summary>What `sindri onyx up` ended with.</summary>
/// <param name="Port">The loopback port Onyx's web server is on.</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="Version">The release the running Onyx reports.</param>
/// <param name="Warnings">Preflight checks that warned and didn't block.</param>
/// <param name="Ran">The steps that did work this run: fetch, config, pull, start.</param>
/// <param name="Skipped">The steps whose inputs and results were already as needed.</param>
public sealed record OnyxUpResponse(int Port, string Url, string Version, IReadOnlyList<string> Warnings, IReadOnlyList<string> Ran, IReadOnlyList<string> Skipped);
