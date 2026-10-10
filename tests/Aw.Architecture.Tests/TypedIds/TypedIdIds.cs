namespace Aw.Architecture.Tests.TypedIds;

/// <summary>Rule ids for the typed-id guard (docs/csharp/typed-ids.md).</summary>
internal static class TypedIdIds
{
    /// <summary>An assembly that declares ids lacks the VogenDefaults block or sets an argument wrong.</summary>
    public const string DefaultsId = "ARCH-ID-001";

    /// <summary>A value object or a typed-id property breaks the recipe.</summary>
    public const string TypesId = "ARCH-ID-002";
}
