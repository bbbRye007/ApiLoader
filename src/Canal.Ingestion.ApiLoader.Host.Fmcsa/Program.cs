using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Hosting;
using Microsoft.Extensions.Logging;

return await new VendorHostBuilder()
    .WithVendorName("FMCSA")
    .WithAdapterFactory((httpClient, loggerFactory) =>
        new FmcsaAdapter(
            httpClient,
            loggerFactory.CreateLogger<FmcsaAdapter>(),
            FmcsaEndpoints.All))
    .WithEndpoints(FmcsaEndpoints.All)
    .RunAsync(args);
