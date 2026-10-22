using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Testing;

/// <summary>Parser output with deterministic defaults, for tests that script a <see cref="FakeSymbolParser"/> (spec §4b.5).</summary>
public static class Parsed
{
    /// <summary>A public method of one parameter-less signature.</summary>
    /// <param name="qualified">The qualified name ('A.Run').</param>
    /// <param name="line">Its first line; it ends two lines later.</param>
    /// <param name="calls">The call sites in its body.</param>
    /// <returns>The parsed symbol.</returns>
    public static ParsedSymbol Method(string qualified, int line, params CallSite[] calls)
    {
        ArgumentNullException.ThrowIfNull(qualified);
        var name = qualified[(qualified.LastIndexOf('.') + 1)..];
        return new ParsedSymbol(name, qualified, SymbolKind.Method, Arity: 0, ParameterTypesText: string.Empty, line, line + 2, Exported: true, $"void {name}()", ShapeHash: "shape-" + qualified, Complexity: 1, Tokens: [], Body: null, calls);
    }

    /// <summary>A public class.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="line">Its first line.</param>
    /// <returns>The parsed symbol.</returns>
    public static ParsedSymbol Class(string name, int line)
        => new(name, name, SymbolKind.Class, Arity: 0, ParameterTypesText: string.Empty, line, line + 9, Exported: true, $"class {name}", ShapeHash: "shape-" + name, Complexity: 0, Tokens: [], Body: null, []);

    /// <summary>A call to a name on 'this' or a bare name.</summary>
    /// <param name="name">The called name.</param>
    /// <param name="line">The line of the call.</param>
    /// <returns>The call site.</returns>
    public static CallSite Call(string name, int line) => new(EdgeKind.Calls, name, Arity: 0, Receiver: string.Empty, line);
}
