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
    /// Compact mode: table of name, version, tags.
    /// Verbose mode: full metadata per endpoint.
    /// </summary>
    public static Command Build(
        IReadOnlyList<EndpointEntry> endpoints,
        string vendorDisplayName)
    {
        var listCommand = new Command("list", "List available endpoints");

        var verboseOption = new Option<bool>("--verbose")
        { Description = "Show detailed endpoint metadata" };
        listCommand.Options.Add(verboseOption);

        listCommand.SetAction(parseResult =>
        {
            var verbose = parseResult.GetValue(verboseOption);

            Console.WriteLine();
            Console.WriteLine($"{vendorDisplayName}:");
            Console.WriteLine(new string('-', 80));

            if (verbose)
            {
                foreach (var e in endpoints)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {e.Name} (v{e.Definition.ResourceVersion})");
                    Console.WriteLine($"    Resource:    {e.Definition.ResourceName}");
                    Console.WriteLine($"    Page size:   {e.Definition.DefaultPageSize?.ToString() ?? "(none)"}");
                    Console.WriteLine($"    HTTP method: {e.Definition.HttpMethod}");
                    if (e.Definition.Description is not null)
                        Console.WriteLine($"    Description: {e.Definition.Description}");
                    Console.WriteLine(e.Definition.DependsOn is not null
                        ? $"    Depends on:  {e.Definition.DependsOn} (auto-fetched when needed)"
                        : $"    Dependencies: None (simple paged)");
                    if (e.Definition.SupportsWatermark)
                        Console.WriteLine($"    Watermark:   Supported (incremental loads)");
                    if (e.Definition.MinTimeSpan.HasValue)
                        Console.WriteLine($"    Min window:  {e.Definition.MinTimeSpan.Value}");
                    if (e.Definition.MaxTimeSpan.HasValue)
                        Console.WriteLine($"    Max window:  {e.Definition.MaxTimeSpan.Value}");
                    Console.WriteLine($"    Lookback:    {e.Definition.DefaultLookbackDays} days");
                }
            }
            else
            {
                Console.WriteLine($"  {"Name",-32} {"Ver",3}  {"Type",-40}");
                Console.WriteLine($"  {"----",-32} {"---",3}  {"----",-40}");
                foreach (var e in endpoints)
                {
                    var tags = new List<string>();
                    if (e.Definition.DependsOn is not null) tags.Add($"Requires: {e.Definition.DependsOn}");
                    else tags.Add("Simple paged");
                    if (e.Definition.SupportsWatermark) tags.Add("Watermark");
                    if (e.Definition.MinTimeSpan.HasValue || e.Definition.MaxTimeSpan.HasValue) tags.Add("Time-window");
                    Console.WriteLine($"  {e.Name,-32} {"v" + e.Definition.ResourceVersion,3}  {string.Join("  ", tags),-40}");
                }
            }
            Console.WriteLine();
            return 0;
        });

        return listCommand;
    }
}
