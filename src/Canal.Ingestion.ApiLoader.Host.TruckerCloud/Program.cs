using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Host.TruckerCloud;
using Canal.Ingestion.ApiLoader.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

var tcSettings = new TruckerCloudSettings();

return await new VendorHostBuilder()
    .WithVendorName("TruckerCloud")
    .WithAdapterFactory((httpClient, loggerFactory) =>
        new TruckerCloudAdapter(
            httpClient,
            tcSettings.ApiUser,
            tcSettings.ApiPassword,
            loggerFactory.CreateLogger<TruckerCloudAdapter>()))
    .WithEndpoints(TruckerCloudEndpoints.All)
    .WithVendorSettings("TruckerCloud", tcSettings)
    .ConfigureAppConfiguration(builder =>
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Canal.Ingestion.ApiLoader.Host.TruckerCloud.hostDefaults.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'Canal.Ingestion.ApiLoader.Host.TruckerCloud.hostDefaults.json' not found. Check the .csproj EmbeddedResource entry.");
        builder.AddJsonStream(stream);
    })
    .RunAsync(args);
