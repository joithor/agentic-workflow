using Aw.Evidence.Core.Judging;
using Aw.Evidence.Infrastructure.Judging;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class UnavailableJudgeContractTests : JudgeContract
{
    protected override IJudge CreateUnavailable() => new UnavailableJudge(NullLogger<UnavailableJudge>.Instance);
}
