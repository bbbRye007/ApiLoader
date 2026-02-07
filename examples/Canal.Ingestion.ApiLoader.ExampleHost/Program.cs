/*
    Program.cs -- Example host application.

    This is a reference implementation showing how to wire up the Canal ingestion framework
    with vendor-specific adapter packages. In production, this would be a scheduled task,
    Azure Function, container, or similar host process.

    The host owns:
      - Cancellation tokens
      - HttpClient lifetime and transport policy
      - Azure credentials and blob container configuration
      - Vendor adapter instantiation (credentials, secrets)
      - Orchestration: which endpoints to fetch, in what order, with what parameters

    The host does NOT own:
      - Fetch retry logic (FetchEngine)
      - Pagination semantics (vendor adapters)
      - Request identity / metadata (core framework)
      - Blob naming conventions (Canal.Storage.Adls)
*/

using Azure.Identity;
using Azure.Storage.Blobs;
using Canal.Storage.Adls;

using Canal.Ingestion.ApiLoader.Vendors.TruckerCloud;
using Canal.Ingestion.ApiLoader.Vendors.Fmcsa;
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


// 1) Cancellation Token
    using var cts = new CancellationTokenSource();
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

// 2) HttpClient
    using var httpClient = new HttpClient{ Timeout = TimeSpan.FromMinutes(3) };

// 3) Azure Blob Container
    var credential = new ClientSecretCredential(adlsTenantId, adlsClientId, adlsClientSecret);
    BlobContainerClient containerClient = ADLSAccess.Create(adlsAccountName, adlsContainerName, credential).ContainerClient;

// 4) Vendor Adapters -- each lives in its own NuGet package
    var tcAdapter = new TruckerCloudAdapter(httpClient, truckerCloudApiUser, truckerCloudApiPassword);
    // var fmcsaAdapter = new FmcsaAdapter(httpClient);

// 5) Factory per vendor
    var tc = new EndpointLoaderFactory(tcAdapter, containerClient, environmentName, maxDop, defaultMaxRetries, minRetryDelayMs);
    // var fmcsa = new EndpointLoaderFactory(fmcsaAdapter, containerClient, environmentName, maxDop, defaultMaxRetries, minRetryDelayMs);

// 6) Fetch endpoints
    var carriers = await tc.Create(TruckerCloudEndpoints.Carriers).Load(cancellationToken: cts.Token, saveBehavior: SaveBehavior.None, saveWatermark: false);
    Console.WriteLine(carriers[0].Content);
