using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Host.TruckerCloud;
using Canal.Ingestion.ApiLoader.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

var tcSettings = new TruckerCloudSettings();

return await new VendorHostBuilder()
    .WithVendorName("TruckerCloud")
    .WithExecutableName("apiloader-truckercloud")
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
            .GetManifestResourceStream("Canal.Ingestion.ApiLoader.Host.TruckerCloud.hostDefaults.json");
        if (stream is not null) builder.AddJsonStream(stream);
    })
    .RunAsync(args);
