namespace Aw.Kernel.Telemetry.Tests;

/// <summary>A record whose compiler-generated ToString prints every member, a secret one included.</summary>
public sealed record LoginRequest(string User, string Password);
