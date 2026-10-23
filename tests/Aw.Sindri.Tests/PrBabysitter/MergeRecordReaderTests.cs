using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;

namespace Aw.Sindri.Tests.PrBabysitter;

/// <summary>The stats' read side of the merge record: rows in file order, a missing file empty, an unreadable line counted and left out.</summary>
public sealed class MergeRecordReaderTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Rows_come_back_in_file_order_with_their_kind_and_fields()
    {
        var read = await Reader("{\"kind\":\"start\",\"at\":\"x\"}\n{\"kind\":\"comment\",\"type\":\"nit\",\"outcome\":\"fixed\"}\n").Read("T-1/pr-babysitter/1", TestContext.Current.CancellationToken);

        Assert.Equal(["start", "comment"], read.Value.Rows.Select(static r => r.Kind), StringComparer.Ordinal);
        Assert.Equal(0, read.Value.Errors);
        Assert.Equal("nit", read.Value.Rows[1].Fields["type"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_file_is_an_empty_record()
    {
        var read = await Reader(string.Empty).Read("T-1/pr-babysitter/1", TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (read.Value.Rows.Count, read.Value.Errors));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_that_is_not_json_or_has_no_kind_counts_as_an_error_and_the_rest_are_read()
    {
        var read = await Reader("not json\n{\"kind\":\"start\"}\n{\"nokind\":1}\n[1]\n{\"kind\":\"merge\"}\n").Read("T-1/pr-babysitter/1", TestContext.Current.CancellationToken);

        Assert.Equal(["start", "merge"], read.Value.Rows.Select(static r => r.Kind), StringComparer.Ordinal);
        Assert.Equal(3, read.Value.Errors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_area_that_cannot_be_read_is_its_error()
    {
        var read = await new MergeRecordReader(new Area(error: true, text: string.Empty)).Read("T-1/pr-babysitter/1", TestContext.Current.CancellationToken);

        Assert.True(read.IsError);
    }

    private static MergeRecordReader Reader(string text) => new(new Area(error: false, text));

    private sealed class Area(bool error, string text) : IRunArea
    {
        public ValueTask<Result<string>> Read(string address, string name, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<string>>(error ? KernelErrors.NotFound.With("unreadable") : text);

        public ValueTask<Result<bool>> Write(string address, string name, string content, bool append, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Result<string> PathFor(string address, string name) => throw new NotSupportedException();
    }
}
