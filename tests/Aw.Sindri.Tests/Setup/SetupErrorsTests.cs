using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The SND-SETUP family (setup-once spec §7.6): seven codes with the status each one answers, and each one's fix line.</summary>
public sealed class SetupErrorsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-SETUP-001", ErrorKind.Invalid, "Pick the repo's top folder")]
    [InlineData("SND-SETUP-002", ErrorKind.Conflict, "Pick another folder")]
    [InlineData("SND-SETUP-003", ErrorKind.Forbidden, "hold the button")]
    [InlineData("SND-SETUP-004", ErrorKind.Conflict, "approve the profile")]
    [InlineData("SND-SETUP-005", ErrorKind.Invalid, "Enter both")]
    [InlineData("SND-SETUP-006", ErrorKind.Conflict, "Press Set up Onyx first")]
    [InlineData("SND-SETUP-007", ErrorKind.Conflict, "approve the profile first")]
    public void Each_setup_code_is_in_the_catalogue_with_its_kind_and_a_fix_line(string code, ErrorKind kind, string fix)
    {
        var error = Assert.Single(SindriErrors.All, e => e.Code == code);

        Assert.Equal(kind, error.Kind);
        Assert.Contains(fix, error.Title, StringComparison.OrdinalIgnoreCase);
    }
}
