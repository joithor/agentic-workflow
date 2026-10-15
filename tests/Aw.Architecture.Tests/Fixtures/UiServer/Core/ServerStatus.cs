namespace Aw.Architecture.Tests.Fixtures.UiServer.Core;

// ARCH-UI-001: a type of the server package the UI must reach only over HTTP.
internal sealed record ServerStatus(bool Running);
