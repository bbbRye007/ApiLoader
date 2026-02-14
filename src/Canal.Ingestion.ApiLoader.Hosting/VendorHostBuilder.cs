using System.CommandLine;

using Azure.Identity;
using Canal.Storage.Adls;

using Canal.Ingestion.ApiLoader.Adapters;
using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Storage;
using Canal.Ingestion.ApiLoader.Hosting.Commands;
using Canal.Ingestion.ApiLoader.Hosting.Configuration;
using Canal.Ingestion.ApiLoader.Hosting.Helpers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Hosting;

/// <summary>
/// Fluent builder for constructing and running a vendor-specific CLI host.
/// This is the single public entry point that vendor host Program.cs files use.
/// </summary>
public sealed class VendorHostBuilder
{
    // ── Builder state ──
    private string _vendorDisplayName = string.Empty;
    private Func<HttpClient, ILoggerFactory, IVendorAdapter>? _adapterFactory;
    private IReadOnlyList<EndpointEntry>? _endpoints;
    private readonly List<Action<IConfigurationBuilder>> _configCallbacks = [];
    private readonly List<(string SectionName, object Target)> _vendorSettingsBindings = [];

    /// <summary>Sets the vendor display name used in help text and logging.</summary>
    public VendorHostBuilder WithVendorName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        _vendorDisplayName = displayName;
        return this;
    }

    /// <summary>
    /// Registers the adapter factory delegate. The hosting library creates the HttpClient
    /// (5-minute timeout, BaseAddress not yet set — adapter constructor sets it) and provides
    /// ILoggerFactory. The vendor host captures its own settings/credentials in the closure.
    /// </summary>
    public VendorHostBuilder WithAdapterFactory(
        Func<HttpClient, ILoggerFactory, IVendorAdapter> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _adapterFactory = factory;
        return this;
    }

    /// <summary>Registers the vendor's endpoint catalog.</summary>
    public VendorHostBuilder WithEndpoints(IReadOnlyList<EndpointEntry> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _endpoints = endpoints;
        return this;
    }

    /// <summary>
    /// Optional: adds vendor-specific configuration sources (e.g., an embedded hostDefaults.json).
    /// Called during configuration building before external appsettings.json and env vars.
    /// Multiple calls accumulate — each callback is invoked in registration order.
    /// </summary>
    public VendorHostBuilder ConfigureAppConfiguration(
        Action<IConfigurationBuilder> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _configCallbacks.Add(callback);
        return this;
    }

    /// <summary>
    /// Optional: registers a vendor-specific settings object to be bound from IConfiguration
    /// during startup.
    /// </summary>
    public VendorHostBuilder WithVendorSettings(string sectionName, object target)
    {
        ArgumentNullException.ThrowIfNull(sectionName);
        ArgumentNullException.ThrowIfNull(target);
        _vendorSettingsBindings.Add((sectionName, target));
        return this;
    }

    /// <summary>
    /// Builds the System.CommandLine root command tree and invokes it with the provided args.
    /// Returns the process exit code (0 = success, 1 = error, 130 = cancelled).
    /// </summary>
    public async Task<int> RunAsync(string[] args)
    {
        // ── 1. Validate builder state ──
        if (string.IsNullOrEmpty(_vendorDisplayName))
            throw new InvalidOperationException("WithVendorName() must be called before RunAsync().");
        if (_adapterFactory is null)
            throw new InvalidOperationException("WithAdapterFactory() must be called before RunAsync().");
        if (_endpoints is null || _endpoints.Count == 0)
            throw new InvalidOperationException("WithEndpoints() must be called with at least one endpoint before RunAsync().");

        // Validate no duplicate endpoint names
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ep in _endpoints)
        {
            if (!seen.Add(ep.Name))
                throw new InvalidOperationException($"Duplicate endpoint name '{ep.Name}' in endpoint catalog.");
        }

        // ── 2. Build IConfiguration ──
        var configBuilder = new ConfigurationBuilder();

        // Vendor-specific config sources first (lowest precedence — embedded defaults)
        foreach (var cb in _configCallbacks) cb(configBuilder);

        // External appsettings.json (optional)
        configBuilder.AddJsonFile("appsettings.json", optional: true);

        // Environment variables (highest non-CLI precedence)
        configBuilder.AddEnvironmentVariables();

        var config = configBuilder.Build();

        // ── 3. Bind shared settings ──
        var loader = new LoaderSettings();
        config.GetSection("Loader").Bind(loader);

        var azSettings = new AzureSettings();
        config.GetSection("Azure").Bind(azSettings);

        // ── 4. Bind vendor-specific settings ──
        foreach (var (sectionName, target) in _vendorSettingsBindings)
            config.GetSection(sectionName).Bind(target);

        // ── 5. Build System.CommandLine RootCommand ──
        var rootCommand = new RootCommand($"{_vendorDisplayName} API Loader");

        // Global options
        var environmentOption = new Option<string?>("--environment")
        { Description = "Environment tag for storage path", Recursive = true };
        environmentOption.Aliases.Add("-e");

        var storageOption = new Option<string?>("--storage")
        { Description = "Storage backend: adls | file", Recursive = true };
        storageOption.Aliases.Add("-s");

        var localStoragePathOption = new Option<string?>("--local-storage-path")
        { Description = "Root folder when --storage file", Recursive = true };

        var maxDopOption = new Option<int?>("--max-dop")
        { Description = "Max parallel requests", Recursive = true };

        var maxRetriesOption = new Option<int?>("--max-retries")
        { Description = "Max retries per request", Recursive = true };

        rootCommand.Options.Add(environmentOption);
        rootCommand.Options.Add(storageOption);
        rootCommand.Options.Add(localStoragePathOption);
        rootCommand.Options.Add(maxDopOption);
        rootCommand.Options.Add(maxRetriesOption);

        // Capture references for closures
        var adapterFactory = _adapterFactory;
        var endpoints = _endpoints;
        var vendorDisplayName = _vendorDisplayName;

        // Infrastructure setup shared by load command actions
        LoadContext BuildLoadContext(ParseResult parseResult, CancellationToken commandToken)
        {
            // a. Snapshot settings so CLI overrides don't mutate the shared instance
            var settings = loader.Snapshot();

            var envVal = parseResult.GetValue(environmentOption);
            if (envVal is not null) settings.Environment = envVal;

            var storageVal = parseResult.GetValue(storageOption);
            if (storageVal is not null) settings.Storage = storageVal;

            var localPathVal = parseResult.GetValue(localStoragePathOption);
            if (localPathVal is not null) settings.LocalStoragePath = localPathVal;

            var maxDopVal = parseResult.GetValue(maxDopOption);
            if (maxDopVal.HasValue) settings.MaxDop = maxDopVal.Value;

            var maxRetriesVal = parseResult.GetValue(maxRetriesOption);
            if (maxRetriesVal.HasValue) settings.MaxRetries = maxRetriesVal.Value;

            // b. Sanitize environment name
            settings.Environment = EnvironmentNameSanitizer.Sanitize(settings.Environment);

            // c-h. Create resources with cleanup on partial failure
            ILoggerFactory? loggerFactory = null;
            CancellationTokenSource? processCts = null;
            CancellationTokenSource? linkedCts = null;
            HttpClient? httpClient = null;
            Action? cleanupHandlers = null;

            try
            {
                // c. Create ILoggerFactory
                loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConfiguration(config.GetSection("Logging"));
                    builder.AddConsole();
                });
                var hostLogger = loggerFactory.CreateLogger("ApiLoader");

                hostLogger.LogInformation(
                    "ApiLoader starting — vendor={Vendor}, environment={Environment}, storage={Storage}, maxDop={MaxDop}",
                    vendorDisplayName, settings.Environment, settings.Storage, settings.MaxDop);

                // d. Cancellation token — link the System.CommandLine token with
                //    process-exit / Ctrl+C signals so either source triggers cancellation.
                //    Both processCts and linkedCts are owned and disposed by LoadContext.
                processCts = new CancellationTokenSource();
                void RequestCancel()
                {
                    try { if (!processCts.IsCancellationRequested) processCts.Cancel(); }
                    catch (ObjectDisposedException) { }
                }

                Action<System.Runtime.Loader.AssemblyLoadContext> unloadingHandler = _ => RequestCancel();
                EventHandler processExitHandler = (_, _) => RequestCancel();
                ConsoleCancelEventHandler cancelKeyPressHandler = (_, e) => { e.Cancel = true; RequestCancel(); };

                System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += unloadingHandler;
                AppDomain.CurrentDomain.ProcessExit += processExitHandler;
                Console.CancelKeyPress += cancelKeyPressHandler;

                cleanupHandlers = () =>
                {
                    System.Runtime.Loader.AssemblyLoadContext.Default.Unloading -= unloadingHandler;
                    AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
                    Console.CancelKeyPress -= cancelKeyPressHandler;
                };

                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    commandToken, processCts.Token);

                // e. Build IIngestionStore
                IIngestionStore store;
                if (settings.Storage.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    hostLogger.LogInformation("Using local file storage at {Path}", settings.LocalStoragePath);
                    store = new LocalFileIngestionStore(settings.LocalStoragePath);
                }
                else
                {
                    var credential = new ClientSecretCredential(
                        azSettings.TenantId, azSettings.ClientId, azSettings.ClientSecret);
                    var containerClient = ADLSAccess.Create(
                        azSettings.AccountName, azSettings.ContainerName, credential).ContainerClient;
                    store = new AdlsIngestionStore(containerClient);
                }

                // f. Create HttpClient
                httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

                // g. Invoke adapter factory
                var adapter = adapterFactory(httpClient, loggerFactory);

                // h. Create EndpointLoaderFactory
                var factory = new EndpointLoaderFactory(
                    adapter, store, settings.Environment,
                    settings.MaxDop, settings.MaxRetries, settings.MinRetryDelayMs, loggerFactory);

                return new LoadContext(
                    loggerFactory, httpClient, linkedCts, processCts,
                    cleanupEventHandlers: cleanupHandlers)
                {
                    Factory = factory,
                    Endpoints = endpoints,
                    Logger = hostLogger,
                    CancellationToken = linkedCts.Token
                };
            }
            catch
            {
                // Clean up partially-created resources in reverse order
                cleanupHandlers?.Invoke();
                linkedCts?.Dispose();
                processCts?.Dispose();
                httpClient?.Dispose();
                loggerFactory?.Dispose();
                throw;
            }
        }

        // Add 'load' command
        var loadCommand = LoadCommandBuilder.Build(endpoints, BuildLoadContext);
        rootCommand.Subcommands.Add(loadCommand);

        // Add 'list' command
        var listCommand = ListCommandBuilder.Build(endpoints, vendorDisplayName);
        rootCommand.Subcommands.Add(listCommand);

        // ── 6. Invoke and return exit code ──
        var cliConfig = new CommandLineConfiguration(rootCommand);
        return await cliConfig.InvokeAsync(args).ConfigureAwait(false);
    }
}
