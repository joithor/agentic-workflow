using Aw.Sindri.Infrastructure;

namespace Aw.Sindri.Tests.Callers;

public sealed class ProverHomeTests
{
    private const string Temp = "/private/var/folders/xy/T";
    private const string State = "/Users/thor/.agentic-workflow";

    [Theory, Trait("Lane", "unit")]
    [InlineData("/private/var/folders/xy/T/aw-p1/home", true)]
    [InlineData("/private/var/folders/xy/T/home", true)]
    [InlineData("/Users/thor/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-api-p1/home", true)]
    [InlineData("/Users/thor/.agentic-workflow/jthorare-agentic-workflow/proof/home", true)]
    [InlineData("/private/var/folders/xy/T", false)]
    [InlineData("/private/var/folders/xy/Temp/home", false)]
    [InlineData("/Users/thor/.agentic-workflow/sindri-app", false)]
    [InlineData("/Users/thor/.agentic-workflow/jthorare-agentic-workflow/proof", false)]
    [InlineData("/Users/thor/.agentic-workflow/jthorare-agentic-workflow/plans/proof/home", false)]
    [InlineData("/Users/thor/.agentic-workflow/proof/home", false)]
    [InlineData("/Users/thor/.cache/aw-p1-nontemp", false)]
    public void Prover_home_rule_accepts_temp_and_proof_folders_only(string home, bool allowed) =>
        Assert.Equal(allowed, ProverHome.Allows(home, Temp, State));

    [Fact, Trait("Lane", "unit")]
    public void A_trailing_separator_on_the_roots_changes_nothing() =>
        Assert.True(ProverHome.Allows("/private/var/folders/xy/T/home", Temp + "/", State + "/"));
}
