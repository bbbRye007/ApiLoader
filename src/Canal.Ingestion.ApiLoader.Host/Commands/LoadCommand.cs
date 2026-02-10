using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Host.Configuration;
using Canal.Ingestion.ApiLoader.Host.Helpers;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Host.Commands;

public static class LoadCommand
{
    public static async Task<int> RunAsync(
        CliArgs cliArgs, EndpointLoaderFactory factory, ILogger logger, CancellationToken ct)
    {
        var vendor   = cliArgs.Positional(0);
        var endpoint = cliArgs.Positional(1);

        if (string.IsNullOrEmpty(vendor) || string.IsNullOrEmpty(endpoint))
        {
            logger.LogError("Usage: apiloader load <vendor> <endpoint> [options]");
            return 1;
        }

        // Resolve endpoint
        var entry = EndpointRegistry.Find(vendor, endpoint);
        if (entry is null)
        {
            logger.LogError("Endpoint '{Endpoint}' not found for vendor '{Vendor}'.", endpoint, vendor);
            var available = EndpointRegistry.EndpointNamesForVendor(vendor);
            if (available.Count > 0)
                logger.LogError("Available endpoints for {Vendor}: {Endpoints}", vendor, string.Join(", ", available));
            else
                logger.LogError("Unknown vendor '{Vendor}'. Known vendors: {Vendors}", vendor, string.Join(", ", EndpointRegistry.Vendors));
            return 1;
        }

        // Parse dates
        DateTimeOffset? startUtc = null, endUtc = null;
        try
        {
            var startRaw = cliArgs.Option("start");
            var endRaw = cliArgs.Option("end");
            if (startRaw is not null) startUtc = FlexibleDateParser.Parse(startRaw);
            if (endRaw is not null)   endUtc = FlexibleDateParser.Parse(endRaw);
        }
        catch (FormatException ex)
        {
            logger.LogError("Invalid date: {Message}", ex.Message);
            return 1;
        }

        // Parse save behavior
        var saveBehaviorRaw = cliArgs.Option("save-behavior") ?? "PerPage";
        if (!Enum.TryParse<SaveBehavior>(saveBehaviorRaw, ignoreCase: true, out var saveBehavior))
        {
            logger.LogError("Invalid --save-behavior '{Value}'. Must be: PerPage, AfterAll, or None.", saveBehaviorRaw);
            return 1;
        }

        var saveWatermark = !cliArgs.Flag("no-save-watermark");
        var pageSize  = cliArgs.IntOption("page-size");
        var maxPages  = cliArgs.IntOption("max-pages");
        var dryRun    = cliArgs.Flag("dry-run");

        // Resolve dependency chain
        List<EndpointRegistry.EndpointEntry> chain;
        try { chain = EndpointRegistry.ResolveDependencyChain(vendor, endpoint); }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Dependency resolution failed: {Message}", ex.Message);
            return 1;
        }

        // Dry run
        if (dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("=== DRY RUN ===");
            Console.WriteLine($"Vendor:        {vendor}");
            Console.WriteLine($"Endpoint:      {entry.Name} (v{entry.Definition.ResourceVersion})");
            Console.WriteLine($"Resource:      {entry.Definition.ResourceName}");
            Console.WriteLine($"Save behavior: {saveBehavior}");
            Console.WriteLine($"Watermark:     {(saveWatermark ? "save" : "skip")}");
            if (startUtc.HasValue) Console.WriteLine($"Start:         {startUtc.Value:O}");
            if (endUtc.HasValue)   Console.WriteLine($"End:           {endUtc.Value:O}");
            if (pageSize.HasValue) Console.WriteLine($"Page size:     {pageSize.Value}");
            if (maxPages.HasValue) Console.WriteLine($"Max pages:     {maxPages.Value}");
            if (chain.Count > 1)
            {
                Console.WriteLine();
                Console.WriteLine("Dependency chain (executed in order):");
                for (int i = 0; i < chain.Count; i++)
                {
                    var isTarget = (i == chain.Count - 1);
                    Console.WriteLine($"  {i + 1}. {chain[i].Name} v{chain[i].Definition.ResourceVersion}{(isTarget ? "  <-- target (saved)" : "  (fetched, not saved)")}");
                }
            }
            Console.WriteLine();
            Console.WriteLine("No data will be fetched.");
            return 0;
        }

        // Execute
        List<FetchResult>? iterationList = null;
        for (int i = 0; i < chain.Count; i++)
        {
            var step = chain[i];
            var isTarget = (i == chain.Count - 1);

            if (!isTarget)
            {
                logger.LogInformation("Auto-fetching dependency: {Endpoint} (unsaved, for iteration list)", step.Name);
                iterationList = await factory.Create(step.Definition).Load(
                    cancellationToken: ct, saveBehavior: SaveBehavior.None, saveWatermark: false
                ).ConfigureAwait(false);
            }
            else
            {
                logger.LogInformation("Loading target endpoint: {Endpoint}", step.Name);
                await factory.Create(step.Definition).Load(
                    iterationList: iterationList,
                    overrideStartUtc: startUtc, overrideEndUtc: endUtc,
                    pageSize: pageSize, maxPages: maxPages,
                    saveBehavior: saveBehavior, saveWatermark: saveWatermark,
                    cancellationToken: ct
                ).ConfigureAwait(false);
            }
        }

        return 0;
    }
}
