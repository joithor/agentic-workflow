using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>
/// The C# parser (spec §5.3): Roslyn syntax only, no compilation, no references, no project loading, nothing of the repo
/// executed. The file's text is parsed on its own thread with a large fixed stack, under a time limit, and walked with a
/// depth cap; a file that trips a guard comes back as a skip ('too-deep', 'timeout', ...), never an error (CI12).
/// </summary>
internal sealed class CSharpParser : ISymbolParser
{
    private static readonly CSharpParseOptions _options = new(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Regular);

    /// <summary>The parser's version word; a new version re-parses every file (spec §5.2).</summary>
    public const string ParserVersion = "csharp-1";

    private readonly ParseLimits _limits;

    /// <summary>Creates the parser with the limits of a real build.</summary>
    public CSharpParser()
        : this(ParseLimits.Default)
    {
    }

    internal CSharpParser(ParseLimits limits)
    {
        _limits = limits;
    }

    /// <inheritdoc />
    public string Version => ParserVersion;

    /// <inheritdoc />
    public bool Handles(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Result<ParseOutcome> Parse(string path, string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BracketsNestBeyond(text, _limits.MaxDepth))
        {
            return Skipped(SkipReasons.TooDeep);
        }

        using var timeout = new CancellationTokenSource(_limits.TimeLimit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var run = new Run();
        var thread = new Thread(() => run.Execute(() => ParseText(text, linked.Token)), _limits.StackBytes) { IsBackground = true, Name = "code-index-parse" };
        thread.Start();

        // A parse that ignores its token past the time limit plus a grace is abandoned, never waited on: a thread cannot be aborted.
        if (!thread.Join(_limits.TimeLimit + TimeSpan.FromSeconds(5)))
        {
            timeout.Cancel();
            return Skipped(SkipReasons.Timeout);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return run.Failure switch
        {
            null => run.Outcome ?? Skipped(SkipReasons.ParserFailed),
            ParseLimitExceededException limit => Skipped(limit.Reason),
            InsufficientExecutionStackException => Skipped(SkipReasons.TooDeep),
            OperationCanceledException => Skipped(SkipReasons.Timeout),
            _ => Skipped(SkipReasons.ParserFailed),
        };
    }

    private ParseOutcome ParseText(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tree = CSharpSyntaxTree.ParseText(text, _options, cancellationToken: cancellationToken);
        var root = tree.GetRoot(cancellationToken);
        if (!NestsWithin(root, _limits.MaxDepth, cancellationToken))
        {
            return new ParseOutcome([], SkipReasons.TooDeep);
        }

        return new ParseOutcome(new CSharpSymbolWalker(tree.GetText(cancellationToken), _limits, cancellationToken).Run(root));
    }

    // An explicit stack, so the check itself cannot overflow on the tree it is checking: it stops as soon as one path is too deep.
    private static bool NestsWithin(SyntaxNode root, int maxDepth, CancellationToken cancellationToken)
    {
        var pending = new Stack<(SyntaxNode Node, int Depth)>();
        pending.Push((root, 0));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (current.Depth > maxDepth)
            {
                return false;
            }

            foreach (var child in current.Node.ChildNodes())
            {
                pending.Push((child, current.Depth + 1));
            }
        }

        return true;
    }

    // Roslyn's parser backtracks over every open parenthesis (cast, lambda or tuple?), so a few thousand nested ones outlast the time limit and
    // ignore the token. A lexical count of open brackets refuses such a file before the parser sees it; it counts strings too, which only
    // matters to a file with a thousand unclosed brackets in one literal, and that ends as a skip, never an error.
    private static bool BracketsNestBeyond(string text, int maxDepth)
    {
        var depth = 0;
        foreach (var c in text)
        {
            if (c is '(' or '[' or '{')
            {
                if (++depth > maxDepth)
                {
                    return true;
                }
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
        }

        return false;
    }

    private static ParseOutcome Skipped(string reason) => new([], reason);

    private sealed class Run
    {
        public ParseOutcome? Outcome { get; private set; }

        public Exception? Failure { get; private set; }

        public void Execute(Func<ParseOutcome> parse)
        {
            try
            {
                Outcome = parse();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Failure = exception;
            }
        }
    }
}
