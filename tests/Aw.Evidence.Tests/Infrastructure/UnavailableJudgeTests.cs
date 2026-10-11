using Aw.Evidence.Infrastructure.Judging;
using Microsoft.Extensions.Logging;

namespace Aw.Evidence.Tests.Infrastructure;

public sealed class UnavailableJudgeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_unavailable_judge_is_not_available_answers_nothing_and_says_so_once()
    {
        var log = new CountingLogger();
        var judge = new UnavailableJudge(log);

        Assert.False(judge.IsAvailable);
        Assert.Null(await judge.CritiqueAsync(null!, TestContext.Current.CancellationToken));   // why: the judge never reads the request
        Assert.Null(await judge.RepairSelectorAsync(null!, TestContext.Current.CancellationToken));   // why: as above

        Assert.Equal(1, log.Informations);
    }

    private sealed class CountingLogger : ILogger<UnavailableJudge>
    {
        public int Informations { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Informations += logLevel == LogLevel.Information ? 1 : 0;
    }
}
