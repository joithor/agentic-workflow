using Aw.CodeIndex.Core.Ports;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// What every <see cref="ISymbolParser"/> honours (spec §11): it handles the paths it covers and no other, and it
/// answers any text, hostile or empty, with an outcome and never an exception. A derived class binds it to the fake and to
/// each real parser and carries the lane.
/// </summary>
public abstract class SymbolParserContract
{
    /// <summary>The parser under test.</summary>
    /// <returns>A parser that handles <see cref="HandledPath"/>.</returns>
    protected abstract ISymbolParser CreateParser();

    /// <summary>A path the parser handles.</summary>
    protected abstract string HandledPath { get; }

    /// <summary>A path no parser of this contract handles.</summary>
    protected virtual string UnhandledPath => "notes.unknown-extension";

    /// <summary>The parser handles its path and not a foreign one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task The_parser_handles_its_own_path_and_not_a_foreign_one()
    {
        var parser = CreateParser();

        Require(parser.Handles(HandledPath), $"Handles({HandledPath}) is true");
        Require(!parser.Handles(UnhandledPath), $"Handles({UnhandledPath}) is false");
        return Task.CompletedTask;
    }

    /// <summary>Text of any shape gets an outcome, never an exception.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task Any_text_gets_an_outcome_and_never_an_exception()
    {
        var parser = CreateParser();
        string[] texts = ["", "x", "class", "class A {", "\"unterminated", "/* never closed", "}}}} ;;;; ((((", "﻿class A { }\r\nclass B { }\n", new string('(', 5_000)];

        foreach (var text in texts)
        {
            var result = parser.Parse(HandledPath, text, CancellationToken.None);
            Require(result.IsSuccess, $"Parse of {text.Length} characters is a success");
            Require(result.Value is not null, "the outcome is not null");
        }

        return Task.CompletedTask;
    }

    /// <summary>The same text parsed twice gives the same symbols.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task The_same_text_parsed_twice_gives_the_same_symbols()
    {
        var parser = CreateParser();
        const string text = "class A { void F() { G(); } void G() { } }";

        var first = parser.Parse(HandledPath, text, CancellationToken.None).Value;
        var second = parser.Parse(HandledPath, text, CancellationToken.None).Value;

        Require(first.Skip == second.Skip && first.Symbols.Select(s => s.Qualified).SequenceEqual(second.Symbols.Select(s => s.Qualified), StringComparer.Ordinal), "two parses of one text agree");
        return Task.CompletedTask;
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {what}");
        }
    }
}
