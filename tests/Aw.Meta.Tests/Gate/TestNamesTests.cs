using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class TestNamesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Failing_tests_are_the_names_on_the_failed_lines_of_an_MTP_log()
    {
        const string Log = "Running tests from x.dll (net10.0|x64)\n"
            + "failed Aw.Meta.Tests.TmpFail.Foo_does_x(value: 3) (12ms)\n"
            + "  from /x/y.dll (net10.0|x64)\n"
            + "  Assert.Equal() Failure\n"
            + "failed Aw.Meta.Tests.TmpFail.Bar (1s 151ms)\n"
            + "failed Aw.Meta.Tests.TmpFail.Bar (0ms)\n"
            + "Test run summary: Failed!\n  failed: 2\n";

        Assert.Equal(["Aw.Meta.Tests.TmpFail.Foo_does_x(value: 3)", "Aw.Meta.Tests.TmpFail.Bar"], TestNames.Failing(Log));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_log_with_no_failed_lines_names_no_tests() =>
        Assert.Empty(TestNames.Failing("Passed!\n  failed: 0\n"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("Foo_does_x", "A.B.Foo_does_x(value: 3)", true)]
    [InlineData("Foo_does_x(value: 3)", "A.B.Foo_does_x(value: 3)", true)]
    [InlineData("B.Foo_does_x", "A.B.Foo_does_x(value: 3)", true)]
    [InlineData("A.B.Foo_does_x(value: 3)", "A.B.Foo_does_x(value: 3)", true)]
    [InlineData("foo_does_x", "A.B.Foo_does_x", true)]
    [InlineData("does_x", "A.B.Foo_does_x", false)]
    [InlineData("Foo", "A.B.Foo_does_x", false)]
    [InlineData("Tests", "A.StackProbeTests.Run", false)]
    [InlineData("StackProbeTests", "A.StackProbeTests.Run", true)]
    [InlineData("StackProbeTests.Run", "A.StackProbeTests.Run", true)]
    [InlineData("Probe", "A.StackProbeTests.Run", false)]
    [InlineData("", "A.B.C", false)]
    public void A_filter_names_a_test_by_equality_or_a_whole_segment_suffix(string filter, string name, bool expected) =>
        Assert.Equal(expected, TestNames.Matches(filter, name));

    [Fact, Trait("Lane", "unit")]
    public void The_bash_runners_failed_suites_are_named_once_from_its_real_log()
    {
        var log = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Gate", "Golden", "bash-runner-one-failed.log"));

        Assert.Equal(["scripts/tests/ci-bash-tests.test.sh"], TestNames.Failing(log));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bash_log_with_only_passing_suites_names_no_tests() =>
        Assert.Empty(TestNames.Failing("PASS  par  scripts/tests/a.test.sh  1s\nbash suites: 2 run, 0 failed, 0 excluded\n"));
}
