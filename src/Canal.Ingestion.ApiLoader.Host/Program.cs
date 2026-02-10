using Azure.Identity;
using Canal.Storage.Adls;

using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Storage;
using Canal.Ingestion.ApiLoader.Host.Commands;
using Canal.Ingestion.ApiLoader.Host.Configuration;
using Canal.Ingestion.ApiLoader.Host.Helpers;

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// ── 1. Configuration: embedded defaults → external appsettings.json → env vars ──
//    (CLI overrides applied programmatically below)
var configBuilder = new ConfigurationBuilder();

// Baked-in defaults from the embedded hostDefaults.json resource
var defaultsStream = Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("Canal.Ingestion.ApiLoader.Host.hostDefaults.json");
if (defaultsStream is not null)
    configBuilder.AddJsonStream(defaultsStream);

// Optional external file next to the exe — for deploy-time overrides without recompiling
configBuilder.AddJsonFile("appsettings.json", optional: true);

// Environment variables override both
configBuilder.AddEnvironmentVariables();

var config = configBuilder.Build();

// ── 2. Bind typed settings from config ──
var loader = new LoaderSettings();
config.GetSection("Loader").Bind(loader);

var tcSettings = new TruckerCloudSettings();
config.GetSection("TruckerCloud").Bind(tcSettings);

var azSettings = new AzureSettings();
config.GetSection("Azure").Bind(azSettings);

// ── 3. Parse CLI args ──
var cliArgs = new CliArgs(args);
var command = cliArgs.Positional(0)?.ToLowerInvariant();

// Show help if no command or --help
if (command is null or "--help" or "-h" or "help")
{
    HelpText.Print();
    return 0;
}

// ── 4. CLI overrides (highest precedence) ──
var envRaw = cliArgs.Option("environment") ?? cliArgs.Option("e");
if (envRaw is not null) loader.Environment = envRaw;

var storageRaw = cliArgs.Option("storage") ?? cliArgs.Option("s");
if (storageRaw is not null) loader.Storage = storageRaw;

var localPathRaw = cliArgs.Option("local-storage-path");
if (localPathRaw is not null) loader.LocalStoragePath = localPathRaw;

var maxDopRaw = cliArgs.IntOption("max-dop");
if (maxDopRaw.HasValue) loader.MaxDop = maxDopRaw.Value;

var maxRetriesRaw = cliArgs.IntOption("max-retries");
if (maxRetriesRaw.HasValue) loader.MaxRetries = maxRetriesRaw.Value;

// ── 5. Sanitize environment name for ADLS blob safety ──
loader.Environment = EnvironmentNameSanitizer.Sanitize(loader.Environment);

// ── 6. Logging ──
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConfiguration(config.GetSection("Logging"));
    builder.AddConsole();
});
var hostLogger = loggerFactory.CreateLogger("ApiLoader");

hostLogger.LogInformation("ApiLoader starting — environment={Environment}, storage={Storage}, maxDop={MaxDop}",
    loader.Environment, loader.Storage, loader.MaxDop);

// ── 7. Cancellation token ──
using var cts = new CancellationTokenSource();
void RequestCancel() { try { if (!cts.IsCancellationRequested) cts.Cancel(); } catch (ObjectDisposedException) { } }
System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += _ => RequestCancel();
AppDomain.CurrentDomain.ProcessExit += (_, _) => RequestCancel();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; RequestCancel(); };

// ── 8. Build storage backend ──
IIngestionStore BuildStore()
{
    if (loader.Storage.Equals("file", StringComparison.OrdinalIgnoreCase))
    {
        hostLogger.LogInformation("Using local file storage at {Path}", loader.LocalStoragePath);
        return new LocalFileIngestionStore(loader.LocalStoragePath);
    }

    // Default: ADLS
    var credential = new ClientSecretCredential(azSettings.TenantId, azSettings.ClientId, azSettings.ClientSecret);
    var containerClient = ADLSAccess.Create(azSettings.AccountName, azSettings.ContainerName, credential).ContainerClient;
    return new AdlsIngestionStore(containerClient);
}

// ── 9. Build factory for a vendor ──
// HttpClient lifetime: CLI app is short-lived — one command per run, then the process exits.
// Each factory gets its own HttpClient that lives for the duration of the command.
EndpointLoaderFactory BuildFactory(string vendor, IIngestionStore store)
{
    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

    if (vendor.Equals("truckercloud", StringComparison.OrdinalIgnoreCase))
    {
        var adapter = new TruckerCloudAdapter(httpClient, tcSettings.ApiUser, tcSettings.ApiPassword, loggerFactory.CreateLogger<TruckerCloudAdapter>());
        return new EndpointLoaderFactory(adapter, store, loader.Environment, loader.MaxDop, loader.MaxRetries, loader.MinRetryDelayMs, loggerFactory);
    }

    if (vendor.Equals("fmcsa", StringComparison.OrdinalIgnoreCase))
    {
        var adapter = new FmcsaAdapter(httpClient, loggerFactory.CreateLogger<FmcsaAdapter>());
        return new EndpointLoaderFactory(adapter, store, loader.Environment, loader.MaxDop, loader.MaxRetries, loader.MinRetryDelayMs, loggerFactory);
    }

    throw new InvalidOperationException($"Unknown vendor '{vendor}'. Known vendors: {string.Join(", ", EndpointRegistry.Vendors)}");
}

// ── 10. Route to command ──
// Strip the command name from args so sub-commands see clean positional args
var subArgs = args.Length > 1 ? new CliArgs(args[1..]) : new CliArgs([]);

try
{
    return command switch
    {
        "list" => ListCommand.Run(subArgs),

        "load" => await LoadCommand.RunAsync(
            subArgs,
            BuildFactory(subArgs.Positional(0) ?? "", BuildStore()),
            hostLogger,
            cts.Token).ConfigureAwait(false),

        "test" => await TestCommand.RunAsync(
            subArgs,
            vendor => BuildFactory(vendor, BuildStore()),
            hostLogger,
            cts.Token).ConfigureAwait(false),

        _ => Error($"Unknown command '{command}'. Run 'apiloader --help' for usage.")
    };
}
catch (OperationCanceledException)
{
    hostLogger.LogWarning("Operation cancelled.");
    return 130;
}
catch (Exception ex)
{
    hostLogger.LogError(ex, "Unhandled error.");
    return 1;
}

static int Error(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
