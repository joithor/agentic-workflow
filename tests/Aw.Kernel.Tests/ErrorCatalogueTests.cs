using Aw.Kernel;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class ErrorCatalogueTests
{
    [Fact]
    public void Kernel_codes_are_unique_well_formed_and_valid()
    {
        Assert.True(ErrorCatalogue.Validate(KernelErrors.All).IsSuccess);
        Assert.Equal(KernelErrors.All.Count, KernelErrors.All.Select(d => d.Code).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("AW-KERNEL-001", ErrorKind.Invalid)]
    [InlineData("AW-KERNEL-002", ErrorKind.NotFound)]
    [InlineData("AW-KERNEL-003", ErrorKind.Conflict)]
    [InlineData("AW-KERNEL-004", ErrorKind.RateLimited)]
    [InlineData("AW-KERNEL-005", ErrorKind.BudgetExceeded)]
    [InlineData("AW-KERNEL-006", ErrorKind.Unavailable)]
    [InlineData("AW-KERNEL-007", ErrorKind.Timeout)]
    [InlineData("AW-KERNEL-008", ErrorKind.Forbidden)]
    public void Kernel_codes_are_stable(string code, ErrorKind kind)
    {
        var definition = Assert.Single(KernelErrors.All, d => d.Code == code);
        Assert.Equal(kind, definition.Kind);
    }

    [Fact]
    public void A_definition_makes_an_error_with_its_code_and_kind()
    {
        var error = KernelErrors.BudgetExceeded.With("token budget 40000 spent");

        Assert.Equal(new Error("AW-KERNEL-005", ErrorKind.BudgetExceeded, "token budget 40000 spent"), error);
    }

    [Fact]
    public void Duplicate_codes_fail_validation_naming_the_code()
    {
        var catalogue = new[]
        {
            new ErrorDefinition("SND-EVOLVE-017", ErrorKind.NotFound, "index missing"),
            new ErrorDefinition("SND-EVOLVE-017", ErrorKind.Invalid, "same code again"),
        };

        var result = ErrorCatalogue.Validate(catalogue);

        Assert.Equal("AW-KERNEL-003", result.Error.Code);
        Assert.Contains("SND-EVOLVE-017", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aw-kernel-001")]
    [InlineData("AW-KERNEL-1")]
    [InlineData("XX-KERNEL-001")]
    [InlineData("AW-KERNEL-\uff10\uff10\uff11")]
    [InlineData("")]
    public void Malformed_codes_fail_validation(string code)
    {
        var result = ErrorCatalogue.Validate([new ErrorDefinition(code, ErrorKind.Invalid, "bad")]);

        Assert.Equal("AW-KERNEL-001", result.Error.Code);
    }
}
