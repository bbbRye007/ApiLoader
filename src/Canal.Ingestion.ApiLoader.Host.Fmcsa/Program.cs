using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

return await new VendorHostBuilder()
    .WithVendorName("FMCSA")
    .WithExecutableName("apiloader-fmcsa")
    .WithAdapterFactory((httpClient, loggerFactory) =>
        new FmcsaAdapter(
            httpClient,
            loggerFactory.CreateLogger<FmcsaAdapter>()))
    .WithEndpoints(FmcsaEndpoints.All)
    .ConfigureAppConfiguration(builder =>
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Canal.Ingestion.ApiLoader.Host.Fmcsa.hostDefaults.json");
        if (stream is not null) builder.AddJsonStream(stream);
    })
    .RunAsync(args);
