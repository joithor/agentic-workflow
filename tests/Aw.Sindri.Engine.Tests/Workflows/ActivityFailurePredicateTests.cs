using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Api.Failure.V1;
using Temporalio.Converters;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Which failures of the start's local 'ReadItemRepo' activity leave 'Repo' unset instead of ending the workflow: every failure of the activity (a timeout included), never a cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ActivityFailurePredicateTests
{
    private static Exception Raised(Failure failure) => DataConverter.Default.FailureConverter.ToException(failure, DataConverter.Default.PayloadConverter);

    [Theory]
    [InlineData("timeout", true)]
    [InlineData("application", true)]
    [InlineData("activity", true)]
    [InlineData("canceled", false)]
    public void Only_a_cancellation_is_not_an_activity_failure(string kind, bool expected)
    {
        var failure = new Failure { Message = kind };
        switch (kind)
        {
            case "timeout": failure.TimeoutFailureInfo = new TimeoutFailureInfo(); break;
            case "application": failure.ApplicationFailureInfo = new ApplicationFailureInfo { Type = "IOException" }; break;
            case "activity": failure.ActivityFailureInfo = new ActivityFailureInfo(); break;
            default: failure.CanceledFailureInfo = new CanceledFailureInfo(); break;
        }

        Assert.Equal(expected, RecipeWorkflow.IsActivityFailure(Raised(failure)));
    }
}
