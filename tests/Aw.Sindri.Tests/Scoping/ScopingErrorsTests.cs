using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Scoping;

public sealed class ScopingErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_scoping_catalogue_is_valid_together_with_sindris_and_the_engines() =>
        Assert.True(ErrorCatalogue.Validate([.. SindriErrors.All, .. WorkflowErrors.All, .. ScopingErrors.All]).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void The_live_codes_are_001_002_004_006_007_008_and_009_and_003_is_retired_and_never_live()
    {
        Assert.Equal(["SND-SCOPING-001", "SND-SCOPING-002", "SND-SCOPING-004", "SND-SCOPING-005", "SND-SCOPING-006", "SND-SCOPING-007", "SND-SCOPING-008", "SND-SCOPING-009"], ScopingErrors.All.Select(e => e.Code), StringComparer.Ordinal);
        Assert.Equal(["SND-SCOPING-003"], ScopingErrors.Retired);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-SCOPING-001", ErrorKind.Invalid)]
    [InlineData("SND-SCOPING-002", ErrorKind.Invalid)]
    [InlineData("SND-SCOPING-004", ErrorKind.Invalid)]
    [InlineData("SND-SCOPING-005", ErrorKind.Invalid)]
    [InlineData("SND-SCOPING-006", ErrorKind.NotFound)]
    [InlineData("SND-SCOPING-007", ErrorKind.Conflict)]
    [InlineData("SND-SCOPING-008", ErrorKind.Unavailable)]
    [InlineData("SND-SCOPING-009", ErrorKind.Unavailable)]
    public void Each_code_has_its_kind(string code, ErrorKind kind) =>
        Assert.Equal(kind, Assert.Single(ScopingErrors.All, e => e.Code == code).Kind);
}
