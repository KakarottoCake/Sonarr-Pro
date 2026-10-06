namespace Sonarr.Api.V5.LibraryTools;

internal static class LibraryOperationGate
{
    internal static readonly SemaphoreSlim Gate = new(1, 1);
}
