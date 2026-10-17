using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>Every task-returning method of a port takes a CancellationToken, so a caller can always cancel it.</summary>
public static class AsyncRules
{
    /// <summary>A Task or ValueTask method on a Core interface with no CancellationToken parameter.</summary>
    public const string Id = "ARCH-ASYNC-001";

    private const string TokenType = "System.Threading.CancellationToken";

    /// <summary>Checks that every task-returning method declared on a Core interface (a port) takes a CancellationToken.</summary>
    /// <param name="types">The types to check.</param>
    /// <param name="scope">The shaped packages in scope.</param>
    /// <returns>One violation per task-returning port method without a token. DisposeAsync is exempt.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types, Scope scope) =>
        types
            .Where(t => t is Interface && Place.Of(t, scope) is { Layer: Layer.Core })
            .SelectMany(t => t.Members.OfType<MethodMember>().Select(m => (Type: t, Method: m)))
            .Where(x => ReturnsTask(x.Method)
                && !x.Method.Name.StartsWith("DisposeAsync(", StringComparison.Ordinal)
                && !x.Method.Parameters.Any(p => p.FullName == TokenType))
            .Select(x => new Violation(Id, $"{x.Type.FullName}.{x.Method.Name} returns a task and takes no CancellationToken",
                "add a trailing CancellationToken cancellationToken parameter and pass it on"));

    private static bool ReturnsTask(MethodMember method)
    {
        var name = method.ReturnType.FullName.Split('`')[0];
        return string.Equals(name, "System.Threading.Tasks.Task", StringComparison.Ordinal)
            || string.Equals(name, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }
}
