namespace Aw.Kernel.Telemetry.Tests;

/// <summary>A record holding another record, for the nested-member case.</summary>
public sealed record Session(string Id, LoginRequest Login);
