using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Host.TruckerCloud;
using Canal.Ingestion.ApiLoader.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

const string EmbeddedDefaultsResource = "Canal.Ingestion.ApiLoader.Host.TruckerCloud.truckerCloudDefaults.json";

// tcSettings is instantiated before config is built. WithVendorSettings registers it for
// later binding during RunAsync. The adapter factory closure captures tcSettings by reference;
// it will hold bound values by the time the factory is invoked.
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
        // Copy into MemoryStream so the native resource stream can be disposed immediately.
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(EmbeddedDefaultsResource)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedDefaultsResource}' not found. Check the .csproj EmbeddedResource entry.");
        var mem = new MemoryStream();
        stream.CopyTo(mem);
        mem.Position = 0;
        builder.AddJsonStream(mem);
    })
    .RunAsync(args);
