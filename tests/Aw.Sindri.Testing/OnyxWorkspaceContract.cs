using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IOnyxWorkspace"/> honours. Run against the fake and the file adapter.</summary>
public abstract class OnyxWorkspaceContract
{
    /// <summary>A rig over an empty workspace.</summary>
    /// <returns>The rig.</returns>
    protected abstract OnyxWorkspaceRig Create();

    /// <summary>A file reads back as written and a missing one as null.</summary>
    [Fact]
    public void A_written_file_reads_back_and_a_missing_one_is_null()
    {
        using var rig = Create();

        Expect.True(rig.Workspace.Read(rig.Paths.Env) is null);
        Expect.True(rig.Workspace.WriteIfChanged(rig.Paths.Env, "A=1\n", secret: true));

        Expect.Equal("A=1\n", rig.Workspace.Read(rig.Paths.Env) ?? "");
    }

    /// <summary>Identical content writes nothing; changed content writes.</summary>
    [Fact]
    public void Write_if_changed_returns_false_for_identical_content()
    {
        using var rig = Create();
        Expect.True(rig.Workspace.WriteIfChanged(rig.Paths.Override, "a\n", secret: false));

        Expect.True(!rig.Workspace.WriteIfChanged(rig.Paths.Override, "a\n", secret: false));
        Expect.True(rig.Workspace.WriteIfChanged(rig.Paths.Override, "b\n", secret: false));
        Expect.Equal("b\n", rig.Workspace.Read(rig.Paths.Override) ?? "");
    }

    /// <summary>A missing state is null.</summary>
    [Fact]
    public void A_missing_state_reads_as_null()
    {
        using var rig = Create();

        Expect.True(rig.Workspace.ReadState() is null);
    }

    /// <summary>A corrupt state is null, never an exception.</summary>
    /// <param name="raw">What the state file holds.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"port\":")]
    [InlineData("{}")]
    [InlineData("")]
    public void A_corrupt_state_reads_as_null(string raw)
    {
        using var rig = Create();
        rig.WriteRawState(raw);

        Expect.True(rig.Workspace.ReadState() is null);
    }

    /// <summary>A state round-trips.</summary>
    [Fact]
    public void State_round_trips()
    {
        using var rig = Create();
        var state = new OnyxSetupState(3002, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Fetch] = "aa", [OnyxSteps.Config] = "bb" });

        rig.Workspace.WriteState(state);

        var read = rig.Workspace.ReadState() ?? throw new Kernel.Testing.ContractViolationException("contract: the state written reads back as null");
        Expect.Equal(3002, read.Port);
        Expect.Equal("aa", read.Steps[OnyxSteps.Fetch]);
        Expect.Equal("bb", read.Steps[OnyxSteps.Config]);
        Expect.True(read.Steps.Count == 2);
    }

    /// <summary>A second lock is refused while the first is held, and allowed once it is released.</summary>
    [Fact]
    public void A_second_lock_is_refused_while_the_first_is_held()
    {
        using var rig = Create();

        var first = rig.Workspace.TryLock();
        Expect.True(first is not null);
        Expect.True(rig.Workspace.TryLock() is null);

        first?.Dispose();
        using var again = rig.Workspace.TryLock();
        Expect.True(again is not null);
    }
}
