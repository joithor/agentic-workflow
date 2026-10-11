using Aw.Evidence.Core.Judging;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeJudgeContractTests : JudgeContract
{
    protected override IJudge CreateUnavailable() => FakeJudge.Unavailable;
}
