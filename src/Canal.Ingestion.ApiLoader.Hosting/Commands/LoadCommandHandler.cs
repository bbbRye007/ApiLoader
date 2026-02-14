using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Executes the load for a single endpoint. Resolves dependency chain, auto-fetches
/// dependencies (unsaved), then loads the target endpoint with user-specified parameters.
/// Functionally equivalent to the current <c>LoadCommand.RunAsync</c> but vendor-agnostic.
/// </summary>
internal static class LoadCommandHandler
{
    /// <summary>
    /// Prints the dry-run execution plan without creating infrastructure.
    /// Called directly from the command handler when --dry-run is set.
    /// </summary>
    public static int ExecuteDryRun(
        EndpointEntry target,
        IReadOnlyList<EndpointEntry> allEndpoints,
        int? pageSize,
        int? maxPages,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        SaveBehavior saveBehavior,
        bool saveWatermark)
    {
        List<EndpointEntry> chain;
        try
        {
            chain = DependencyResolver.Resolve(target, allEndpoints);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Dependency resolution failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("=== DRY RUN ===");
        Console.WriteLine($"Endpoint:      {target.Name} (v{target.Definition.ResourceVersion})");
        Console.WriteLine($"Resource:      {target.Definition.ResourceName}");
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

    /// <summary>
    /// Runs the load operation. Called by the System.CommandLine handler for each
    /// endpoint subcommand.
    /// </summary>
    /// <returns>Exit code: 0 = success, 1 = error.</returns>
    public static async Task<int> ExecuteAsync(
        EndpointEntry target,
        IReadOnlyList<EndpointEntry> allEndpoints,
        EndpointLoaderFactory factory,
        ILogger logger,
        CancellationToken cancellationToken,
        int? pageSize,
        int? maxPages,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        SaveBehavior saveBehavior,
        bool saveWatermark,
        string bodyParamsJson)
    {
        // Resolve dependency chain
        List<EndpointEntry> chain;
        try
        {
            chain = DependencyResolver.Resolve(target, allEndpoints);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Dependency resolution failed: {Message}", ex.Message);
            return 1;
        }

        // Execute chain — pass iterationList from each step into the next
        List<FetchResult>? iterationList = null;
        for (int i = 0; i < chain.Count; i++)
        {
            var step = chain[i];
            var isTarget = (i == chain.Count - 1);

            if (!isTarget)
            {
                logger.LogInformation("Auto-fetching dependency: {Endpoint} (unsaved, for iteration list)", step.Name);
                try
                {
                    iterationList = await factory.Create(step.Definition).Load(
                        iterationList: iterationList,
                        cancellationToken: cancellationToken,
                        saveBehavior: SaveBehavior.None,
                        saveWatermark: false
                    ).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Dependency {Endpoint} (step {Step}/{Total}) failed", step.Name, i + 1, chain.Count);
                    return 1;
                }

                if (iterationList is null || iterationList.Count == 0)
                    logger.LogWarning("Dependency {Endpoint} returned zero results — target may produce empty output", step.Name);
            }
            else
            {
                logger.LogInformation("Loading target endpoint: {Endpoint}", step.Name);
                await factory.Create(step.Definition).Load(
                    iterationList: iterationList,
                    overrideStartUtc: startUtc,
                    overrideEndUtc: endUtc,
                    pageSize: pageSize,
                    maxPages: maxPages,
                    saveBehavior: saveBehavior,
                    saveWatermark: saveWatermark,
                    bodyParamsJson: bodyParamsJson,
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            }
        }

        return 0;
    }
}
