using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>The default Admin group's id of Sindri's own Onyx database, scripted. Remembers the projects it was asked about.</summary>
public sealed class FakeOnyxDatabase : IOnyxDatabase
{
    /// <summary>Gets or sets the id answered for a project with a database; null answers that no stack of Sindri's runs here.</summary>
    public int? Group { get; set; } = 1;

    /// <summary>Gets or sets an error the read returns instead.</summary>
    public Error? Failure { get; set; }

    /// <summary>Gets the project names the read was asked about.</summary>
    public IList<string> Asked { get; } = [];

    /// <inheritdoc />
    public Result<int?> DefaultAdminGroup(ComposeProject project, CancellationToken cancellationToken)
    {
        if (!ComposeProject.IsOurs(project.Name))
        {
            return SindriErrors.OnyxComposeFailed.With($"refusing to act on \"{project.Name}\": it isn't a compose project Sindri names");
        }

        Asked.Add(project.Name);
        return Failure is { } error ? error : Results.Success(Group);
    }
}
