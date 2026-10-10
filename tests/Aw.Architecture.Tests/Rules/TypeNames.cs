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
        var ns = type.Namespace.FullName;
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
        type.Namespace.FullName.Length == 0 ? TopLevel(type) : $"{type.Namespace.FullName}.{TopLevel(type)}";
}
