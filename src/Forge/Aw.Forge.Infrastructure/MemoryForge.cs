using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.World;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure;

/// <summary>The in-memory forge: the same rules as the file forge over a world that lives and dies with the process. A push is simulated against its branch tips.</summary>
internal sealed class MemoryForge(ForgeHosts hosts, IRedactor redactor)
    : WorldForge(ForgeKind.File, hosts, new MemoryWorldStore(), redactor, git: null);
