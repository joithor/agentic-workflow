using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>Name helpers over ArchUnitNET types.</summary>
public static class TypeNames
{
    /// <summary>The top-level type a (possibly nested or compiler-generated) type belongs to, without its namespace.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The outermost declaring type's name, without generic arity.</returns>
    public static string TopLevel(IType type)
    {
        var ns = NamespaceOf(type);
        var full = type.FullName;
        var local = ns.Length > 0 && full.StartsWith(ns + ".", StringComparison.Ordinal) ? full[(ns.Length + 1)..] : full;
        var outer = local.Split('+', '/')[0];
        var tick = outer.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? outer : outer[..tick];
    }

    /// <summary>The namespace-qualified top-level type name, for messages.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Namespace plus top-level type name.</returns>
    public static string Qualified(IType type) =>
        NamespaceOf(type).Length == 0 ? TopLevel(type) : $"{NamespaceOf(type)}.{TopLevel(type)}";

    /// <summary>
    /// The type's namespace, or empty. ArchUnitNET gives some targets no namespace at all, such as the generic
    /// parameter of a file-local generic method (the request-delegate generator's Cast&lt;T&gt;).
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The namespace's full name, or the empty string.</returns>
    public static string NamespaceOf(IType type) => type.Namespace?.FullName ?? string.Empty;

    /// <summary>The short name of the type's assembly, or empty.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The assembly name without version or culture.</returns>
    public static string AssemblyOf(IType type) => type.Assembly?.Name?.Split(',')[0] ?? string.Empty;
}
