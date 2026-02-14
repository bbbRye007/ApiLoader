using System.CommandLine;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Builds the <c>list</c> command that displays available endpoints for this vendor.
/// </summary>
internal static class ListCommandBuilder
{
    /// <summary>
    /// Creates the <c>list</c> command with a <c>--verbose</c> flag option.
    /// </summary>
    public static Command Build(
        IReadOnlyList<EndpointEntry> endpoints,
        string vendorDisplayName)
    {
        var listCommand = new Command("list", "List available endpoints");

        // Placeholder — fully implemented in commit 6

        return listCommand;
    }
}
