using Aw.Sindri.Core;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The command line of 'sindri context map review' (and 'context filemap accept|reject'): a mistake is a usage error before anything is sent.</summary>
public sealed class ReviewArgumentsTests
{
    private static ReviewCommand Ok(params string[] args)
    {
        var parsed = ReviewArguments.Parse(args);
        Assert.True(parsed.IsSuccess, parsed.IsError ? parsed.Error.Message : string.Empty);
        return parsed.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void No_arguments_is_the_interactive_loop_and_json_lists_the_queue()
    {
        Assert.Equal((ReviewMode.Interactive, false), (Ok().Mode, Ok().Json));
        Assert.Equal((ReviewMode.Queue, true), (Ok("--json").Mode, Ok("--json").Json));
        Assert.Equal(ReviewMode.Queue, Ok("--list").Mode);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_limit_means_the_queue_like_list_does_because_the_queue_tells_you_to_raise_it()
    {
        var capped = Ok("--limit", "500");

        Assert.Equal((ReviewMode.Queue, "500", false), (capped.Mode, capped.Limit, capped.Json));
        Assert.Equal(ReviewMode.Queue, Ok("--limit", "5", "--json").Mode);
    }

    [Fact, Trait("Lane", "unit")]
    public void History_and_export_take_their_flags()
    {
        Assert.Equal(ReviewMode.History, Ok("--history").Mode);
        Assert.True(Ok("--history", "--json").Json);
        var export = Ok("--export", "/tmp/r.json", "--json");
        Assert.Equal((ReviewMode.Export, "/tmp/r.json", true), (export.Mode, export.Path, export.Json));
        Assert.Null(Ok("--export").Path);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--undo", "ovr_0123456789abcdef", "undo")]
    [InlineData("--drop", "ovr_0123456789abcdef", "drop")]
    public void Undo_and_drop_name_an_override(string flag, string id, string verb)
    {
        var command = Ok(flag, id);

        Assert.Equal(ReviewMode.Request, command.Mode);
        Assert.Equal((verb, id), (command.Request!.Verb, command.Request.Override));
    }

    [Fact, Trait("Lane", "unit")]
    public void Apply_the_bulk_accept_repoint_and_the_direct_verbs_build_one_request()
    {
        var apply = Ok("--apply", "/tmp/r.json").Request!;
        var bulk = Ok("--accept-deterministic").Request!;
        var repoint = Ok("--repoint", "ovr_0123456789abcdef", "--to", "feature:tabs").Request!;
        var accept = Ok("accept", "feature:tabs").Request!;
        var acceptLink = Ok("accept", "edge:0123456789abcdef").Request!;
        var rename = Ok("rename", "feature:tabs", "Tab", "manager").Request!;
        var move = Ok("move", "surface:command/x", "--under", "feature:tabs").Request!;
        var merge = Ok("merge", "feature:a", "--into", "feature:b").Request!;
        var add = Ok("add", "feature:new", "New", "thing", "--under", "feature:tabs").Request!;
        var delete = Ok("delete", "feature:a").Request!;

        Assert.Equal(("apply-file", "/tmp/r.json"), (apply.Verb, apply.Path));
        Assert.Equal("accept-deterministic", bulk.Verb);
        Assert.Equal(("repoint", "ovr_0123456789abcdef", "feature:tabs"), (repoint.Verb, repoint.Override, repoint.Node));
        Assert.Equal(("accept", "feature:tabs", null), (accept.Verb, accept.Node, accept.Link));
        Assert.Equal(("accept", null, "edge:0123456789abcdef"), (acceptLink.Verb, acceptLink.Node, acceptLink.Link));
        Assert.Equal(("rename", "Tab manager"), (rename.Verb, rename.Label));
        Assert.Equal(("move", "feature:tabs"), (move.Verb, move.Other));
        Assert.Equal(("merge", "feature:b"), (merge.Verb, merge.Other));
        Assert.Equal(("add", "New thing", "feature:tabs"), (add.Verb, add.Label, add.Other));
        Assert.Equal("delete", delete.Verb);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_takes_one_id_or_a_comma_separated_list()
    {
        var one = Ok("--undo", "ovr_0123456789abcdef").Request!;
        var list = Ok("--undo", "ovr_0123456789abcdef,ovr_fedcba9876543210").Request!;

        Assert.Equal(("ovr_0123456789abcdef", null), (one.Override, one.Overrides));
        Assert.Null(list.Override);
        Assert.Equal(["ovr_0123456789abcdef", "ovr_fedcba9876543210"], list.Overrides, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--undo")]
    [InlineData("--apply")]
    [InlineData("--repoint", "ovr_0123456789abcdef")]
    [InlineData("accept")]
    [InlineData("move", "feature:a")]
    [InlineData("merge", "feature:a")]
    [InlineData("rename", "feature:a")]
    [InlineData("frobnicate", "feature:a")]
    [InlineData("--history", "--export")]
    public void A_missing_value_or_an_unknown_word_is_a_usage_error_naming_the_forms(params string[] args)
    {
        var parsed = ReviewArguments.Parse(args);

        Assert.True(parsed.IsError);
        Assert.Equal(SindriErrors.UnknownVerb.Code, parsed.Error.Code);
        Assert.Contains("context map review", parsed.Error.Message, StringComparison.Ordinal);
    }
}
