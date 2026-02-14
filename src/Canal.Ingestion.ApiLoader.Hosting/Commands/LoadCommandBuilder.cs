using System.CommandLine;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Hosting.Configuration;

namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Builds the <c>load</c> command with one subcommand per endpoint. Each subcommand's
/// options are conditionally derived from the endpoint's <see cref="EndpointDefinition"/> metadata.
/// </summary>
internal static class LoadCommandBuilder
{
    /// <summary>
    /// Creates the <c>load</c> command. For each endpoint in <paramref name="endpoints"/>,
    /// generates a subcommand with conditionally-present options based on the endpoint's
    /// definition metadata (see AD-004 derivation rules).
    /// </summary>
    public static Command Build(
        IReadOnlyList<EndpointEntry> endpoints,
        LoaderSettings loaderSettings,
        Func<ParseResult, LoadContext> contextFactory)
    {
        var loadCommand = new Command("load", "Load a single endpoint (dependencies auto-resolved)");

        // Placeholder — fully implemented in commit 6
        foreach (var entry in endpoints)
        {
            var endpointCommand = new Command(entry.Name, BuildDescription(entry));
            loadCommand.Subcommands.Add(endpointCommand);
        }

        return loadCommand;
    }

    private static string BuildDescription(EndpointEntry entry)
    {
        var def = entry.Definition;
        var desc = $"{entry.Name} (v{def.ResourceVersion})";
        if (def.Description is not null) desc += $" — {def.Description}";
        desc += $"\nResource: {def.ResourceName} | {def.HttpMethod} | Page size: {def.DefaultPageSize?.ToString() ?? "(none)"}";
        if (def.RequiresIterationList && def.DependsOn is not null)
            desc += $"\nDepends on: {def.DependsOn} (auto-fetched)";
        if (def.SupportsWatermark)
        {
            desc += "\nWatermark: supported";
            if (def.MinTimeSpan.HasValue) desc += $" | Min window: {def.MinTimeSpan.Value}";
            if (def.MaxTimeSpan.HasValue) desc += $" | Max window: {def.MaxTimeSpan.Value}";
        }
        return desc;
    }
}
