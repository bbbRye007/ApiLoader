
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
        1) The PaginationOptions class is not generalized to handle every concievable pagination shceme out there.
            So as the Canal.Ingestion.ApiLoader platform grows, PaginationOptions will have to be generalized to suit the requirement of new vendor pagination schemes.
        2) Most of these types of APIs just use query parameters and/or request headers, but it is possible to come across a vendor that requires these be passed in as part of a request-body.
            If that happens, work will need to be done to add support for that in the Model classes and the FetchEngine.

        Other than these two possible scenarios, adding a new vendor should not require changes to any code in "vendor-neutral" namespaces.
*/

///////////// MOCK HOST LOGIC //////////////

/*
    TODO:
*/
using Azure.Identity;
using Azure.Storage.Blobs;
using Canal.Storage.Adls;

using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;

using Microsoft.Extensions.Configuration;
// TODO: Remove config files containing "secrets" - these should be managed with Azure Key Vault
    var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();
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

    /*  DEPENDENCY 1) Cancellation Token
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

    /* DEPENDENCY 2) HttpClient
        is injected because it's a shared transport with connection pooling.
        The host (the top-level entrypoint that runs the app -- aka Program.cs / container / etc) owns lifetime + policy (timeouts, decompression, proxy/DNS behavior, retries/logging),
        so adapters stay focused on building requests, and tests can inject a fake client/handler if needed.
    */
        using var httpClient = new HttpClient{ Timeout = TimeSpan.FromMinutes(3) };

    /* DEPENDENCY 3) "Azure Stuff" TokenCredential and BlobContainerClient
        NOTE: ClientSecretCredential works, but it's not the long-term plan.
        Prefer Managed Identity (Azure) or DefaultAzureCredential (dev/CI) to avoid secret rotation and config leakage.
        Or do --> var credential = new DefaultAzureCredential(); // <<-- when possible

        BlobContainerClient is created in the host layer because the host owns "environment wiring":
        endpoint/account/container selection, credential choice, retry/logging policy, and lifetime.
        Keeping that out of services/adapters avoids hidden config/secrets and makes storage easy to swap/mock.
    */
        var credential = new ClientSecretCredential(adlsTenantId, adlsClientId, adlsClientSecret);
        BlobContainerClient containerClient = ADLSAccess.Create(adlsAccountName, adlsContainerName, credential).ContainerClient;

    /* DEPENDENCY 4) VendorAdapter
        adapter layer owns all the quirks for a given vendor. its feasible fpr one vendor to have multiple adapters if they have some endpoints with certain quirks and other endpoints with other quirks, but "vendor" felt like the right abstraction level during this POC/MVP
    */
        var tcAdapter = new TruckerCloudAdapter(httpClient, truckerCloudApiUser, truckerCloudApiPassword);
        // var fmcsaAdapter = new FmcsaAdapter(httpClient);

    /*
        READY TO GO -- create a factory per vendor, then use factory.Create(definition).Load(...) for any endpoint.
    */

    var tc = new EndpointLoaderFactory(tcAdapter, containerClient, environmentName, maxDop, defaultMaxRetries, minRetryDelayMs);
    // var fmcsa = new EndpointLoaderFactory(fmcsaAdapter, containerClient, environmentName, maxDop, defaultMaxRetries, minRetryDelayMs);

    // ── FMCSA examples (all simple paged endpoints) ──────────────────────

    // await fmcsa.Create(FmcsaEndpoints.InspectionsPerUnit).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.InsHistAllWithHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.ActPendInsurAllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.AuthHistoryAllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.Boc3AllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.CarrierAllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.CompanyCensus).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.CrashFile).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.InsurAllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.InspectionsAndCitations).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.RejectedAllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.RevocationAllHistory).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.SpecialStudies).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.VehicleInspectionsAndViolations).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.VehicleInspectionFile).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.SmsInputMotorCarrierCensus).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.SmsInputInspection).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.SmsInputViolation).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);
    // await fmcsa.Create(FmcsaEndpoints.SmsInputCrash).Load(maxNrPagesBeforeAbort: 5, cancellationToken: cts.Token);

    // return;

    // ── TruckerCloud examples ────────────────────────────────────────────

    // DO NOT USE VehicleIgnition (see TruckerCloudEndpoints.VehicleIgnition comment)
    // For ONE Vehicle the result was a json document 900K lines long! No date parameters to filter by!

    // carriers is a required input for dependent endpoints. fetch it first, then pass results as priorResults.
    var carriers = await tc.Create(TruckerCloudEndpoints.Carriers).Load(cancellationToken: cts.Token, saveBehavior: SaveBehavior.None, saveWatermark: false);
    Console.WriteLine(carriers[0].Content);
    // await tc.Create(TruckerCloudEndpoints.SafetyEvents).Load(priorResults: carriers, overrideStartUtc: DateTimeOffset.Now.AddDays(-30), overrideEndUtc: DateTimeOffset.Now.AddDays(-5), saveWatermark: true, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.SafetyEvents).Load(priorResults: carriers, cancellationToken: cts.Token);

    // var vehicles = await tc.Create(TruckerCloudEndpoints.Vehicles).Load(cancellationToken: cts.Token, saveBehavior: SaveBehavior.None);

    // THIS ENDPOINT IS A SLOOOOW!!! FOR ONE out of almost 1000 vehicles...

    // await tc.Create(TruckerCloudEndpoints.GpsMiles).Load(priorResults: carriers, cancellationToken: cts.Token);  // <<-- NOT FOR GENERAL POLLING!!

    // await tc.Create(TruckerCloudEndpoints.ZipCodeMiles).Load(priorResults: carriers, overrideStartUtc: DateTimeOffset.Now.AddDays(-150), overrideEndUtc: DateTimeOffset.Now.AddDays(-90), saveWatermark: true, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.RadiusOfOperation).Load(priorResults: carriers, overrideStartUtc: DateTimeOffset.Now.AddDays(-150), overrideEndUtc: DateTimeOffset.Now.AddDays(-90), saveWatermark: true, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.GpsMiles).Load(priorResults: carriers, overrideStartUtc: DateTimeOffset.Now.AddDays(-150), overrideEndUtc: DateTimeOffset.Now.AddDays(-90), saveWatermark: true, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.Drivers).Load(priorResults: carriers, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.RiskScores).Load(priorResults: carriers, cancellationToken: cts.Token);

    // careful with watermarked (incremental) endpoints:
    // overriding the enddate to 90 days ago while also saving the watermark file will cause the next incremental load to pick up from 90 days ago.
    // for one off backfills, pass param saveWatermark: false
    // await tc.Create(TruckerCloudEndpoints.ZipCodeMiles).Load(priorResults: carriers, overrideStartUtc: DateTimeOffset.Now.AddDays(-150), overrideEndUtc: DateTimeOffset.Now.AddDays(-90), saveWatermark: true, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.ZipCodeMiles).Load(priorResults: carriers, cancellationToken: cts.Token); // normal incremental mode should pick up from 90 days ago through today.

    // await tc.Create(TruckerCloudEndpoints.RadiusOfOperation).Load(priorResults: carriers, overrideStartUtc: DateTimeOffset.Now.AddDays(-150), overrideEndUtc: DateTimeOffset.Now.AddDays(-90), saveWatermark: true, cancellationToken: cts.Token);
    // await tc.Create(TruckerCloudEndpoints.RadiusOfOperation).Load(priorResults: carriers, cancellationToken: cts.Token); // normal incremental mode should pick up from 90 days ago through today.
