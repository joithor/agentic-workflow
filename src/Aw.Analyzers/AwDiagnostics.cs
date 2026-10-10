using Microsoft.CodeAnalysis;

namespace Aw.Analyzers;

/// <summary>
/// The repo-specific rules (spec §4b.6, .agents/rules/csharp.md). Every message names the rule id, the measured
/// value, the limit and the fix (spec §8). Severities live in the root .editorconfig.
/// </summary>
internal static class AwDiagnostics
{
    private const string Category = "Aw.Design";
    private const string HelpLink = "https://github.com/jthorare/agentic-workflow/blob/main/.agents/rules/csharp.md";

    /// <summary>Limit: lines per file.</summary>
    internal const int MaxFileLines = 300;

    /// <summary>Limit: parameters per method.</summary>
    internal const int MaxParameters = 5;

    /// <summary>Split signal: more constructor dependencies than this.</summary>
    internal const int ConstructorSplitAt = 5;

    /// <summary>Limit: constructor dependencies.</summary>
    internal const int MaxConstructorDependencies = 7;

    /// <summary>Limit: type parameters per generic type or method.</summary>
    internal const int MaxTypeParameters = 3;

    internal static readonly DiagnosticDescriptor RawId = Rule(
        "AW1001",
        "No raw ids in public Core or Features signatures",
        "AW1001: '{0}' is a raw {1} id in a public signature of {2}; ids are strongly typed (spec §4b.3). Use a typed id (e.g. ItemId, docs/csharp/typed-ids.md) instead of {1}.");

    internal static readonly DiagnosticDescriptor NullForgivingReason = Rule(
        "AW1002",
        "The null-forgiving operator needs a reason",
        "AW1002: '{0}' uses '!' with no reason; every '!' needs a '// why: <reason>' comment on its line or directly above the statement (spec §4b.3). Add the comment, or remove the '!' by making the nullability true.");

    internal static readonly DiagnosticDescriptor CrossSlice = Rule(
        "AW1003",
        "A slice never references another slice's types",
        "AW1003: slice '{0}' references '{1}' from slice '{2}'; slices are isolated (spec §4b.1). Move the shared logic into Core as a domain service or a port.");

    internal static readonly DiagnosticDescriptor EndpointServiceCalls = Rule(
        "AW1004",
        "An endpoint lambda calls exactly one service",
        "AW1004: this endpoint lambda makes {0} IService<,>.Handle calls; the limit is exactly 1 (spec §4b.2). Bind the input, call one service and map its Result; put any other logic in that service.");

    internal static readonly DiagnosticDescriptor FileLength = Rule(
        "AW1005",
        "A file has at most 300 lines",
        "AW1005: this file has {0} lines; the limit is {1} (spec §4b.6). Split it: move a type or a group of members into its own file.");

    internal static readonly DiagnosticDescriptor ParameterCount = Rule(
        "AW1006",
        "A method has at most 5 parameters",
        "AW1006: '{0}' has {1} parameters; the limit is {2} (spec §4b.6). Group related parameters into a request record.");

    internal static readonly DiagnosticDescriptor ConstructorDependencies = Rule(
        "AW1007",
        "A constructor has at most 7 dependencies",
        "AW1007: the constructor of '{0}' takes {1} dependencies; the limit is {2} (spec §4b.2). Split the class: one use case per service.");

    internal static readonly DiagnosticDescriptor ConstructorSplitSignal = new(
        "AW1008",
        "More than 5 constructor dependencies is a split signal",
        "AW1008: the constructor of '{0}' takes {1} dependencies, over the split signal of {2} (spec §4b.2). Consider splitting the class; it becomes AW1007 over 7.",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    internal static readonly DiagnosticDescriptor GenericArity = Rule(
        "AW1009",
        "A generic type or method has at most 3 type parameters",
        "AW1009: '{0}' has {1} type parameters; the limit is {2} (spec §4b.3). Group the types into one record or split the abstraction.");

    private static DiagnosticDescriptor Rule(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: HelpLink);
}
