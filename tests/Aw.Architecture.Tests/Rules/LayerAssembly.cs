namespace Aw.Architecture.Tests.Rules;

/// <summary>A Core, Features or Infrastructure assembly's package root and the layer it holds.</summary>
/// <param name="Root">The package root, e.g. Aw.Judge.</param>
/// <param name="Layer">The layer the assembly holds.</param>
public sealed record LayerAssembly(string Root, Layer Layer);
