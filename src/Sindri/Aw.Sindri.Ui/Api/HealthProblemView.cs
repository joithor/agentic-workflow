namespace Aw.Sindri.Ui.Api;

/// <summary>One problem in the status's health (UI spec E-14): its code and its words, shown as served.</summary>
/// <param name="Code">The SND code.</param>
/// <param name="Text">The problem in words ("index failed", "profile changed").</param>
/// <param name="Clause">The problem as a clause of the health sentence; null before P2.</param>
internal sealed record HealthProblemView(string Code, string Text, string? Clause = null);
