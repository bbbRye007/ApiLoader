using System.CommandLine;
using System.CommandLine.Parsing;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Hosting.Helpers;

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
        Func<ParseResult, CancellationToken, LoadContext> contextFactory)
    {
        var loadCommand = new Command("load", "Load a single endpoint (dependencies auto-resolved)");

        foreach (var entry in endpoints)
        {
            var endpointCommand = BuildEndpointCommand(entry, endpoints, contextFactory);
            loadCommand.Subcommands.Add(endpointCommand);
        }

        return loadCommand;
    }

    private static Command BuildEndpointCommand(
        EndpointEntry entry,
        IReadOnlyList<EndpointEntry> allEndpoints,
        Func<ParseResult, CancellationToken, LoadContext> contextFactory)
    {
        var def = entry.Definition;
        var endpointCommand = new Command(entry.Name, BuildDescription(entry));

        // ── Always-present options ──
        var maxPagesOption = new Option<int?>("--max-pages")
        { Description = "Stop after N pages per request" };

        var saveBehaviorOption = new Option<string>("--save-behavior")
        { Description = "PerPage | AfterAll | None", DefaultValueFactory = _ => "PerPage" };

        var dryRunOption = new Option<bool>("--dry-run")
        { Description = "Show execution plan without fetching" };

        endpointCommand.Options.Add(maxPagesOption);
        endpointCommand.Options.Add(saveBehaviorOption);
        endpointCommand.Options.Add(dryRunOption);

        // ── Conditionally-present options ──
        Option<int?>? pageSizeOption = null;
        if (def.DefaultPageSize is not null)
        {
            pageSizeOption = new Option<int?>("--page-size")
            { Description = $"Override default page size [default: {def.DefaultPageSize}]" };
            endpointCommand.Options.Add(pageSizeOption);
        }

        Option<DateTimeOffset?>? startUtcOption = null;
        Option<DateTimeOffset?>? endUtcOption = null;
        Option<bool>? noSaveWatermarkOption = null;
        if (def.SupportsWatermark)
        {
            startUtcOption = new Option<DateTimeOffset?>("--start-utc")
            {
                Description = "Start of time window (default: from watermark)",
                CustomParser = ParseDateTimeOffset
            };

            endUtcOption = new Option<DateTimeOffset?>("--end-utc")
            {
                Description = "End of time window (default: now)",
                CustomParser = ParseDateTimeOffset
            };

            noSaveWatermarkOption = new Option<bool>("--no-save-watermark")
            { Description = "Skip saving watermark after load" };

            endpointCommand.Options.Add(startUtcOption);
            endpointCommand.Options.Add(endUtcOption);
            endpointCommand.Options.Add(noSaveWatermarkOption);
        }

        Option<string>? bodyParamsJsonOption = null;
        if (def.HttpMethod == HttpMethod.Post)
        {
            bodyParamsJsonOption = new Option<string>("--body-params-json")
            { Description = "JSON body for POST request [default: {}]", DefaultValueFactory = _ => "{}" };
            endpointCommand.Options.Add(bodyParamsJsonOption);
        }

        // ── Handler ──
        // Capture all option references for the closure
        var capturedEntry = entry;
        var capturedAllEndpoints = allEndpoints;

        endpointCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var maxPages = parseResult.GetValue(maxPagesOption);
            var saveBehaviorRaw = parseResult.GetValue(saveBehaviorOption) ?? "PerPage";
            var dryRun = parseResult.GetValue(dryRunOption);
            var pageSize = pageSizeOption is not null ? parseResult.GetValue(pageSizeOption) : null;
            var startUtc = startUtcOption is not null ? parseResult.GetValue(startUtcOption) : null;
            var endUtc = endUtcOption is not null ? parseResult.GetValue(endUtcOption) : null;
            var noSaveWatermark = noSaveWatermarkOption is not null && parseResult.GetValue(noSaveWatermarkOption);
            var bodyParamsJson = bodyParamsJsonOption is not null
                ? parseResult.GetValue(bodyParamsJsonOption) ?? "{}"
                : "{}";

            if (!Enum.TryParse<SaveBehavior>(saveBehaviorRaw, ignoreCase: true, out var saveBehavior))
            {
                Console.Error.WriteLine(
                    $"Invalid --save-behavior '{saveBehaviorRaw}'. Must be: PerPage, AfterAll, or None.");
                return 1;
            }

            // Dry-run does not require adapter/storage — skip infrastructure setup
            if (dryRun)
            {
                return LoadCommandHandler.ExecuteDryRun(
                    capturedEntry, capturedAllEndpoints, pageSize, maxPages,
                    startUtc, endUtc, saveBehavior, !noSaveWatermark);
            }

            LoadContext? ctx = null;
            try
            {
                ctx = contextFactory(parseResult, cancellationToken);
                return await LoadCommandHandler.ExecuteAsync(
                    capturedEntry,
                    capturedAllEndpoints,
                    ctx.Factory,
                    ctx.Logger,
                    ctx.CancellationToken,
                    pageSize,
                    maxPages,
                    startUtc,
                    endUtc,
                    saveBehavior,
                    saveWatermark: !noSaveWatermark,
                    bodyParamsJson).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Operation cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
            finally
            {
                ctx?.Dispose();
            }
        });

        return endpointCommand;
    }

    /// <summary>
    /// Custom parser for DateTimeOffset? using FlexibleDateParser.
    /// Preserves exact parsing behaviour of the current CLI.
    /// </summary>
    private static DateTimeOffset? ParseDateTimeOffset(ArgumentResult result)
    {
        var token = result.Tokens.Count > 0 ? result.Tokens[0].Value : null;
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            return FlexibleDateParser.Parse(token);
        }
        catch (FormatException ex)
        {
            result.AddError(ex.Message);
            return null;
        }
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
