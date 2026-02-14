using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

const string EmbeddedDefaultsResource = "Canal.Ingestion.ApiLoader.Host.Fmcsa.hostDefaults.json";

return await new VendorHostBuilder()
    .WithVendorName("FMCSA")
    .WithAdapterFactory((httpClient, loggerFactory) =>
        new FmcsaAdapter(
            httpClient,
            loggerFactory.CreateLogger<FmcsaAdapter>()))
    .WithEndpoints(FmcsaEndpoints.All)
    .ConfigureAppConfiguration(builder =>
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(EmbeddedDefaultsResource)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedDefaultsResource}' not found. Check the .csproj EmbeddedResource entry.");
        builder.AddJsonStream(stream);
    })
    .RunAsync(args);
