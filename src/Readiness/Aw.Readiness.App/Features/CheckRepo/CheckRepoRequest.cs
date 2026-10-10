namespace Aw.Readiness.App.Features.CheckRepo;

/// <summary>The CheckRepo request: which checkout to check, and whether to ask the repo host.</summary>
/// <param name="Root">The repo root.</param>
/// <param name="UseHost">False for --no-remote: host criteria are unknown instead of read.</param>
internal sealed record CheckRepoRequest(string Root, bool UseHost);
