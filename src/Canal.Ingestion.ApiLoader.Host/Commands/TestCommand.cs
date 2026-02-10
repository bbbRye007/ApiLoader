using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Host.Configuration;
using Canal.Ingestion.ApiLoader.Host.Helpers;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Host.Commands;

public static class TestCommand
{
    public static async Task<int> RunAsync(
        CliArgs cliArgs, Func<string, EndpointLoaderFactory> factoryForVendor, ILogger logger, CancellationToken ct)
    {
        var vendor   = cliArgs.Option("vendor") ?? cliArgs.Option("v");
        var maxPages = cliArgs.IntOption("max-pages");

        var runTc    = vendor is null || vendor.Equals("truckercloud", StringComparison.OrdinalIgnoreCase);
        var runFmcsa = vendor is null || vendor.Equals("fmcsa", StringComparison.OrdinalIgnoreCase);

        if (!runTc && !runFmcsa)
        {
            logger.LogError("Unknown vendor '{Vendor}'. Known vendors: {Vendors}", vendor, string.Join(", ", EndpointRegistry.Vendors));
            return 1;
        }

        if (runTc)
        {
            logger.LogInformation("=== TruckerCloud test suite ===");
            var tc = factoryForVendor("truckercloud");
            var now = DateTimeOffset.UtcNow;
            var overMin = now.AddDays(-14);
            var overMax = now.AddDays(-7);

            var carriers =
            await tc.Create(TruckerCloudEndpoints.CarriersV4)         .Load(cancellationToken: ct, maxPages: maxPages, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.VehiclesV4)         .Load(cancellationToken: ct, maxPages: maxPages, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.DriversV4)          .Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.RiskScoresV4)       .Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.SafetyEventsV5)     .Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.RadiusOfOperationV4).Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.GpsMilesV4)         .Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.ZipCodeMilesV4)     .Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await tc.Create(TruckerCloudEndpoints.TripsV5)            .Load(cancellationToken: ct, maxPages: maxPages, iterationList: carriers, overrideStartUtc: overMin, overrideEndUtc: overMax, saveWatermark: true, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        }

        if (runFmcsa)
        {
            logger.LogInformation("=== FMCSA test suite ===");
            var fmcsa = factoryForVendor("fmcsa");
            var fmcsaMaxPages = maxPages ?? 5;

            await fmcsa.Create(FmcsaEndpoints.InspectionsPerUnit)             .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.InsHistAllWithHistory)          .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.ActPendInsurAllHistory)         .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.AuthHistoryAllHistory)          .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.Boc3AllHistory)                 .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.CarrierAllHistory)              .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.CompanyCensus)                  .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.CrashFile)                      .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.InsurAllHistory)                .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.InspectionsAndCitations)        .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.RejectedAllHistory)             .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.RevocationAllHistory)           .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.SpecialStudies)                 .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.VehicleInspectionsAndViolations).Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.VehicleInspectionFile)          .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.SmsInputMotorCarrierCensus)     .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.SmsInputInspection)             .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.SmsInputViolation)              .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
            await fmcsa.Create(FmcsaEndpoints.SmsInputCrash)                  .Load(maxPages: fmcsaMaxPages, cancellationToken: ct, saveBehavior: SaveBehavior.PerPage).ConfigureAwait(false);
        }

        logger.LogInformation("Test suite completed.");
        return 0;
    }
}
