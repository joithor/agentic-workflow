namespace Aw.Sindri.Core.Health;

/// <summary>One thing that keeps the health from 'healthy' (UI spec E-14).</summary>
/// <param name="Code">Its catalogue code, for example 'SND-VIEW-001'.</param>
/// <param name="Text">The sentence to show, with its fix.</param>
public sealed record HealthProblem(string Code, string Text);
