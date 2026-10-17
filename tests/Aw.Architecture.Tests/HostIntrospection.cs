using System.Reflection;
using Aw.Sindri.Core.Commands;

namespace Aw.Architecture.Tests;

/// <summary>What the Sindri host's rules read by name: its command catalog and the string literals of a namespace's methods.</summary>
internal static class HostIntrospection
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The Sindri host assembly.</summary>
    public static Assembly Host { get; } = Production.Assemblies.Single(a => a.GetName().Name == Solution.AssemblyOf("Aw.Sindri.Host"));

    /// <summary>The Sindri host's command catalog (internal to the host, so made by name).</summary>
    /// <returns>The catalog.</returns>
    public static ICommandCatalog Catalog()
    {
        var type = Host.GetType("Aw.Sindri.Host.Catalog.CommandCatalog", throwOnError: true)!;
        return (ICommandCatalog)Activator.CreateInstance(type, nonPublic: true)!;
    }

    /// <summary>
    /// The string literals loaded by the methods of <paramref name="ns"/> and its nested namespaces in <paramref name="assembly"/>
    /// (lambdas and state machines included: they are nested types in the same namespace), read from each method body's
    /// ldstr instructions.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="ns">The namespace.</param>
    /// <returns>Each literal with the type that loads it.</returns>
    public static List<(string Type, string Literal)> LiteralsIn(Assembly assembly, string ns) =>
        [.. assembly.GetTypes()
            .Where(t => t.Namespace is { } n && (n == ns || n.StartsWith(ns + ".", StringComparison.Ordinal)))
            .SelectMany(t => t.GetMethods(AllDeclared).Cast<MethodBase>().Concat(t.GetConstructors(AllDeclared))
                .SelectMany(m => Ldstr(m).Select(l => (t.FullName ?? t.Name, l))))
            .Distinct()];

    private static IEnumerable<string> Ldstr(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        for (var i = 0; i + 4 < il.Length; i++)
        {
            const byte ldstr = 0x72;
            const int userStringTable = 0x70;
            if (il[i] != ldstr)
            {
                continue;
            }

            var operand = BitConverter.ToInt32(il, i + 1);
            if (operand >> 24 != userStringTable)
            {
                continue;
            }

            string? literal = null;
            try
            {
                literal = method.Module.ResolveString(operand);
            }
            catch (ArgumentException)
            {
                // a byte that looked like ldstr inside another operand, not an instruction
            }

            if (literal is not null)
            {
                yield return literal;
            }
        }
    }
}
