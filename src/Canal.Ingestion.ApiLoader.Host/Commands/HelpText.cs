namespace Canal.Ingestion.ApiLoader.Host.Commands;

public static class HelpText
{
    public static void Print()
    {
        Console.WriteLine("""
        ApiLoader - Canal Ingestion API Data Loader

        Usage: apiloader <command> [options]

        Commands:
          load <vendor> <endpoint>   Load a single endpoint (dependencies auto-resolved)
          test                       Run the standard endpoint test suite
          list                       List available vendors and endpoints

        Common options:
          --environment, -e <name>   Environment tag for storage path (default: UNDEFINED)
          --storage, -s <type>       Storage backend: adls | file (default: adls)
          --local-storage-path <dir> Root folder when --storage file
          --max-dop <n>              Max parallel requests (default: 8)
          --max-retries <n>          Max retries per request (default: 5)

        Load options:
          --start <date>             Start of time window (flexible: 2026-01-15, 01/15/2026, etc.)
          --end <date>               End of time window
          --page-size <n>            Override default page size
          --max-pages <n>            Stop after N pages
          --save-behavior <v>        PerPage | AfterAll | None (default: PerPage)
          --no-save-watermark        Skip saving watermark
          --dry-run                  Show execution plan without fetching

        Test options:
          --vendor, -v <name>        Run only one vendor (default: all)
          --max-pages <n>            Limit pages per endpoint

        List options:
          --vendor, -v <name>        Filter to one vendor
          --verbose                  Show detailed endpoint metadata

        Examples:
          apiloader load truckercloud CarriersV4
          apiloader load truckercloud DriversV4 --max-pages 3 -e STAGING
          apiloader load truckercloud SafetyEventsV5 --start 2026-01-01 --end 2026-01-15
          apiloader load fmcsa CompanyCensus --max-pages 5 --storage file
          apiloader test --vendor fmcsa --max-pages 2
          apiloader list --vendor truckercloud --verbose
        """);
    }
}
