namespace Aw.Kernel;

/// <summary>
/// An expected failure: a stable catalogue code ('AW-…', and 'SND-…' once sindri is ported), its
/// kind, and a message for people. Callers branch on <see cref="Kind"/> or <see cref="Code"/>, never on the message.
/// </summary>
/// <remarks>
/// Make errors with <see cref="ErrorDefinition.With"/> from a package's catalogue, so every code is one
/// <see cref="ErrorCatalogue.Validate"/> has seen. The constructor stays public because JSON reading and the
/// catalogue tests construct errors directly; the in-repo analyzer (spec §4b.6, Aw.Analyzers) is where a ban on
/// 'new Error(...)' outside catalogues belongs.
/// </remarks>
/// <param name="Code">The catalogue code, for example 'AW-KERNEL-002'.</param>
/// <param name="Kind">What kind of failure this is.</param>
/// <param name="Message">What happened, for people.</param>
public sealed record Error(string Code, ErrorKind Kind, string Message);
