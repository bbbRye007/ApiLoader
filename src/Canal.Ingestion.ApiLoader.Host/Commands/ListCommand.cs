using Canal.Ingestion.ApiLoader.Host.Configuration;
using Canal.Ingestion.ApiLoader.Host.Helpers;

namespace Canal.Ingestion.ApiLoader.Host.Commands;

public static class ListCommand
{
    public static int Run(CliArgs cliArgs)
    {
        var vendor = cliArgs.Option("vendor") ?? cliArgs.Option("v");
        var verbose = cliArgs.Flag("verbose");

        var vendors = vendor is not null
            ? [vendor]
            : EndpointRegistry.Vendors;

        foreach (var v in vendors)
        {
            var entries = EndpointRegistry.ForVendor(v);
            if (entries.Count == 0)
            {
                Console.Error.WriteLine($"Unknown vendor: {v}");
                Console.Error.WriteLine($"Known vendors: {string.Join(", ", EndpointRegistry.Vendors)}");
                continue;
            }

            Console.WriteLine();
            Console.WriteLine($"{char.ToUpperInvariant(v[0])}{v[1..]}:");
            Console.WriteLine(new string('-', 80));

            if (verbose)
            {
                foreach (var e in entries)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {e.Name} (v{e.Definition.ResourceVersion})");
                    Console.WriteLine($"    Resource:    {e.Definition.ResourceName}");
                    Console.WriteLine($"    Page size:   {e.Definition.DefaultPageSize?.ToString() ?? "(none)"}");
                    Console.WriteLine($"    HTTP method: {e.Definition.HttpMethod}");
                    if (e.Description is not null)
                        Console.WriteLine($"    Description: {e.Description}");
                    Console.WriteLine(e.DependsOn is not null
                        ? $"    Depends on:  {e.DependsOn} (auto-fetched when needed)"
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
                foreach (var e in entries)
                {
                    var tags = new List<string>();
                    if (e.DependsOn is not null) tags.Add($"Requires: {e.DependsOn}");
                    else tags.Add("Simple paged");
                    if (e.Definition.SupportsWatermark) tags.Add("Watermark");
                    if (e.Definition.MinTimeSpan.HasValue || e.Definition.MaxTimeSpan.HasValue) tags.Add("Time-window");
                    Console.WriteLine($"  {e.Name,-32} {"v" + e.Definition.ResourceVersion,3}  {string.Join("  ", tags),-40}");
                }
            }
        }
        Console.WriteLine();
        return 0;
    }
}
