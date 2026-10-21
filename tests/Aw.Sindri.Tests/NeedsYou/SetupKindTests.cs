using System.Text.Json;
using Aw.Sindri.Core.NeedsYou;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The 'setup' kind of a needs-you row and its subject (setup-once spec §7.3): written as 'setup'.</summary>
public sealed class SetupKindTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_kind_and_the_subject_kind_are_written_as_setup()
    {
        Assert.Equal("\"setup\"", JsonSerializer.Serialize(Enum.Parse<NeedsYouKind>("Setup")));
        Assert.Equal("\"setup\"", JsonSerializer.Serialize(Enum.Parse<SubjectKind>("Setup")));
    }
}
