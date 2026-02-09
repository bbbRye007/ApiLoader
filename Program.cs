
/*
    Program.cs is throwaway - it's just an example of how to write the actual host layer once these libraries are published as nuget packages.

    TODOS:
        Use this console app as a starting place to build two class libraries that can be added to projects via nuget

      NOTE - make sure any NEW code follows the async await pattern shown in this POC -- aka always use ConfigureAwait(false):
        await someAsyncMethod(canecllationToken).ConfigureAwait(false); // This is best practice in async class libraries (no UI to worry about)

      1) add standard logging throughout (following best practices)
      1) Create Class Library --> Canal.Storage.Adls is small and simple, just a few classes in this namespace
      2) Create Class Library --> Canal.Ingestion.ApiLoader (but not down to individual vendors) is dependent on Canal.Storage.Adls and contains all the rest of the namespaces (excluing program.cs which currently emulates a host process)
      3+) Create Class Libraries WITH Vendor Specifics - one per vendor.
///////// CLEAN UP COMMENTARY^^^^^^^^^^^^^^^^^^^^^
/// ALSO -- Consider trying to making the engine "stream back" fetch responses so they can be saved to ADLS BEFORE the total batch of fetches is complete..
/// EXAMPLE -- VehicleIgnition endpoint brings back million plus line json documents.. several minutes per vehicle, 100-ish vehicles means --
///
///
    To get to Production MVP:
      - Build the MVP Host/Orchestration Layer -- just one or more windows scheduled tasks that call a console app with parameters.
            New console app called Telematics Fetch requires nuget package Canal.Ingestion.ApiLoader

            -- add logic to the new console app to handle fetching and saving to adls (see below program.cs code for examples of how that works)
            IMPORTANT (maybe not for day 1, we'll have to talk)
                DO NOT USE config files to store secrets!!!!!
                 -- use Azure Key Vault

            1) instantiate and set up cancellation token to pass through all async method calls
            2) instantiate HttpClient
            3) instantiate BlobContainerClient containerClient
            4) instantiate the TruckerCloud vendorAdapter
            5) instantiate an EndpointLoaderFactory for the vendor
            6) use factory.Create(EndpointDefinition) to create loaders and call Load()
*/

/*
    STEPS TO ADD ANOTHER VENDOR TO THE MIX:

    1) ADD A NEW ADAPTER CLASS RIGHT NEXT TO TruckerCloudAdapter in namespace Canal.Ingestion.ApiLoader.Adapters."NewVendorName"
    2) Add a static catalog class (like TruckerCloudEndpoints or FmcsaEndpoints) with EndpointDefinition fields for each endpoint
    3) Use EndpointLoaderFactory + factory.Create(definition).Load(...) to fetch data

    For common patterns (simple paged, carrier-dependent, carrier+time-window), use the built-in RequestBuilders helpers.
    For truly bizarre endpoints, write a custom BuildRequestsDelegate inline in the catalog -- the loader never sees the difference.

    LIKELY MONKEY WRENCHES:
        1) Most of these types of APIs just use query parameters and/or request headers, but it is possible to come across a vendor that requires these be passed in as part of a request-body.
            If that happens, work will need to be done to add support for that in the Model classes and the FetchEngine.

        Other than this scenario, adding a new vendor should not require changes to any code in "vendor-neutral" namespaces.
*/

using Azure.Identity;
using Azure.Storage.Blobs;
using Canal.Storage.Adls;

using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
// TODO: Remove config files containing "secrets" - these should be managed with Azure Key Vault
    var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();

// LOGGING: Build a LoggerFactory from config. Console provider writes to stdout during dev.
// To change log levels without recompiling, add a "Logging" section to appsettings.json.
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddConfiguration(config.GetSection("Logging"));
        builder.AddConsole();
    });
    var hostLogger = loggerFactory.CreateLogger("Program");
    hostLogger.LogInformation("Application starting");
    string truckerCloudApiUser     = config["AppSettings:API_USER"]                 ?? string.Empty;
    string truckerCloudApiPassword = config["AppSettings:API_PW"]                   ?? string.Empty;
    string adlsContainerName       = config["AppSettings:AZURE_CONTAINER_NAME"]     ?? string.Empty;
    string adlsAccountName         = config["Appsettings:AZURE_ACCOUNT_NAME"]       ?? string.Empty;
    string adlsTenantId            = config["Appsettings:AZURE_TENANT_ETL_ID"]      ?? string.Empty;
    string adlsClientId            = config["Appsettings:AZURE_CLIENT_ETL_ID"]      ?? string.Empty;
    string adlsClientSecret        = config["Appsettings:AZURE_CLIENT_ETL_SECRET"]  ?? string.Empty;

    string environmentName        = "POC";
    int defaultMaxRetries         = 5;
    int minRetryDelayMs           = 100;
    int maxDop                    = 8;


// BULLD Canal.Ingestion.ApiLoader DEPENDEICIES: CancellationToken, HttpClient, BlobContainerClient, VendorAdapter

    /*  "Cancellation Token"
        Best-effort for handling unexpected shutdown with async operations running
        Triggered on:
            container/service stop (SIGTERM scenarios)
            process exit
            CTRL+C or optionally Q or ESC from console (nice in the debugger)

        It's a Shutdown race -- the cancellation token may already be disposed by the time ProcessExit/Unloading events fire.
    */
    using var cts = new CancellationTokenSource(); // Pass cancellation all the way thru like a good developer :D
    void RequestCancel(){try{ if (!cts.IsCancellationRequested) cts.Cancel(); } catch (ObjectDisposedException) {}}
    System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += _ => RequestCancel();
    AppDomain.CurrentDomain.ProcessExit += (_, __) => RequestCancel();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; RequestCancel(); };
    _ = Task.Run(() =>
    {
        while (!cts.IsCancellationRequested)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape) { RequestCancel(); break; }
        }
    });

    /* "Azure Stuff" TokenCredential and BlobContainerClient
        NOTE: ClientSecretCredential works, but it's not the long-term plan.
        Prefer Managed Identity (Azure) or DefaultAzureCredential (dev/CI) to avoid secret rotation and config leakage.
        Or do --> var credential = new DefaultAzureCredential(); // <<-- when possible

        BlobContainerClient is created in the host layer because the host owns "environment wiring":
        endpoint/account/container selection, credential choice, retry/logging policy, and lifetime.
        Keeping that out of services/adapters avoids hidden config/secrets and makes storage easy to swap/mock.
    */
    var credential = new ClientSecretCredential(adlsTenantId, adlsClientId, adlsClientSecret);
    BlobContainerClient containerClient = ADLSAccess.Create(adlsAccountName, adlsContainerName, credential).ContainerClient;


    // TEST TC ENDPOINTS (HttpClient with Using block, Vendor Adapter, Vendpr EndpointFactory)
    var tcHttpClient = new HttpClient{ Timeout = TimeSpan.FromMinutes(5) }; // REMEMBER TO WRAP HttpClient in a Using Block!
    using ( tcHttpClient )
    {
        var tcAdapter = new TruckerCloudAdapter(tcHttpClient, truckerCloudApiUser, truckerCloudApiPassword, loggerFactory.CreateLogger<TruckerCloudAdapter>());
        var tcEndpoints = new EndpointLoaderFactory(tcAdapter, containerClient, environmentName, maxDop, defaultMaxRetries, minRetryDelayMs, loggerFactory);

        var now = DateTimeOffset.UtcNow;
        var overMin = now.AddDays(-14);
        var overMax = now.AddDays(-7);

        var overMin1D = DateTimeOffset.Parse("2026-02-04");
        var overMax1D = overMin1D.AddHours(23).AddMinutes(59).AddSeconds(59);

        var carriers =
        await tcEndpoints.Create(TruckerCloudEndpoints.CarriersV4)         .Load(cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.VehiclesV4)         .Load(cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.DriversV4)          .Load(cancellationToken: cts.Token, iterationList: carriers, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.RiskScoresV4)       .Load(cancellationToken: cts.Token, iterationList: carriers, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.SafetyEventsV5)     .Load(cancellationToken: cts.Token, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.RadiusOfOperationV4).Load(cancellationToken: cts.Token, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.GpsMilesV4)         .Load(cancellationToken: cts.Token, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.ZipCodeMilesV4)     .Load(cancellationToken: cts.Token, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        await tcEndpoints.Create(TruckerCloudEndpoints.TripsV5)            .Load(cancellationToken: cts.Token, iterationList: carriers, overrideStartUtc: overMin1D, overrideEndUtc: overMax1D, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
    }

// TEST FMCSA ENDPOINTS (HttpClient with Using block, Vendor Adapter, Vendpr EndpointFactory)
    var fmcsaHttpClient = new HttpClient{ Timeout = TimeSpan.FromMinutes(5) }; // REMEMBER TO WRAP HttpClient in a Using Block!
    using(fmcsaHttpClient)
    {
        var fmcsaAdapter = new FmcsaAdapter(fmcsaHttpClient, loggerFactory.CreateLogger<FmcsaAdapter>());
        var fmcsaEndpoints = new EndpointLoaderFactory(fmcsaAdapter, containerClient, environmentName, maxDop, defaultMaxRetries, minRetryDelayMs, loggerFactory);
        await fmcsaEndpoints.Create(FmcsaEndpoints.InspectionsPerUnit)             .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.InsHistAllWithHistory)          .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.ActPendInsurAllHistory)         .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.AuthHistoryAllHistory)          .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.Boc3AllHistory)                 .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.CarrierAllHistory)              .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.CompanyCensus)                  .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.CrashFile)                      .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.InsurAllHistory)                .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.InspectionsAndCitations)        .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.RejectedAllHistory)             .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.RevocationAllHistory)           .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.SpecialStudies)                 .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.VehicleInspectionsAndViolations).Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.VehicleInspectionFile)          .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.SmsInputMotorCarrierCensus)     .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.SmsInputInspection)             .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.SmsInputViolation)              .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
        await fmcsaEndpoints.Create(FmcsaEndpoints.SmsInputCrash)                  .Load(maxPages: 5, cancellationToken: cts.Token, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false); // first 5 pages
    }

