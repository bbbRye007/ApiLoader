# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the entire solution
dotnet build ApiLoader.sln

# Build a specific project
dotnet build src/Canal.Ingestion.ApiLoader/Canal.Ingestion.ApiLoader.csproj

# Run the TruckerCloud host
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud

# Run the FMCSA host
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.Fmcsa
```

Target framework is .NET 10.0 with nullable reference types and implicit usings enabled. There are no tests in the repository currently.

### CLI Usage

Each vendor host provides a `System.CommandLine`-based CLI with two commands:

```bash
# List all endpoints for a vendor
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- list
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.Fmcsa -- list

# Load a specific endpoint (dependencies auto-resolved)
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- load CarriersV4 --storage file
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- load SafetyEventsV5 --dry-run

# Global options (apply to all commands)
--environment, -e    Environment tag for storage path
--storage, -s        Storage backend: adls | file
--local-storage-path Root folder when --storage file
--max-dop            Max parallel requests
--max-retries        Max retries per request

# Always-present load options (apply to every load subcommand)
--max-pages          Stop after N pages
--save-behavior      PerPage | AfterAll | None
--dry-run            Show execution plan without fetching

# Conditional load options (present only when endpoint metadata enables them)
--page-size          Override default page size (if endpoint has DefaultPageSize)
--start-utc          Start of time window (if endpoint supports watermark)
--end-utc            End of time window (if endpoint supports watermark)
--no-save-watermark  Skip saving watermark (if endpoint supports watermark)
--body-params-json   JSON body for POST request (if endpoint uses POST)
```

## Architecture

This is a vendor-agnostic API ingestion engine that fetches data from external APIs and persists it to Azure Data Lake Storage (ADLS) or local filesystem.

### Projects

- **Canal.Ingestion.ApiLoader** — Core library: engine, models, adapters interface, storage interface
- **Canal.Ingestion.ApiLoader.Hosting** — Shared hosting library: `VendorHostBuilder`, CLI command builders, configuration, helpers
- **Canal.Ingestion.ApiLoader.Host.TruckerCloud** — Thin Exe host for TruckerCloud vendor (~28 lines)
- **Canal.Ingestion.ApiLoader.Host.Fmcsa** — Thin Exe host for FMCSA vendor (~20 lines)
- **Canal.Ingestion.ApiLoader.TruckerCloud** — Vendor adapter for TruckerCloud API (authenticated, paginated)
- **Canal.Ingestion.ApiLoader.Fmcsa** — Vendor adapter for FMCSA public transportation data API
- **Canal.Storage.Adls** — Azure Blob Storage read/write utilities

### Dependency Graph

```
Host.TruckerCloud → Hosting + TruckerCloud adapter
Host.Fmcsa → Hosting + Fmcsa adapter
Hosting → Core ApiLoader + Canal.Storage.Adls
TruckerCloud adapter → Core ApiLoader
Fmcsa adapter → Core ApiLoader
Core ApiLoader → Canal.Storage.Adls
```

### Host Architecture

Each vendor host is a thin `Program.cs` that uses `VendorHostBuilder` (fluent builder pattern) to:
1. Register the vendor name and adapter factory (`Func<HttpClient, ILoggerFactory, IVendorAdapter>`)
2. Register the endpoint catalog (`IReadOnlyList<EndpointEntry>`)
3. Optionally bind vendor-specific settings (e.g., `TruckerCloudSettings`)
4. Load embedded `hostDefaults.json` defaults

`VendorHostBuilder.RunAsync(args)` builds the configuration stack (embedded defaults → `appsettings.json` → env vars → CLI args), constructs the `System.CommandLine` root command with `load` and `list` subcommands, and invokes it.

CLI options on `load` subcommands are **derived from endpoint metadata** — e.g., `--start-utc`/`--end-utc` only appear for endpoints where `SupportsWatermark == true`, `--body-params-json` only for POST endpoints.

### Execution Pipeline

1. **Program.cs (vendor host)** — Configures `VendorHostBuilder` with adapter factory and endpoint catalog
2. **VendorHostBuilder** — Builds configuration, CLI commands, and infrastructure (store, HttpClient, adapter)
3. **LoadCommandHandler** — Resolves dependency chain, auto-fetches dependencies, loads target endpoint
4. **EndpointLoaderFactory** → creates **EndpointLoader** per endpoint
5. **EndpointLoader** — Orchestrates a load: manages time windows, watermarks, builds requests via `EndpointDefinition.BuildRequests`
6. **FetchEngine** — Executes HTTP requests with retry logic and configurable parallelism
7. **IVendorAdapter** — Applied at each step for vendor-specific URI construction, auth headers, response interpretation, pagination
8. **IIngestionStore** — Persists payloads and metadata (`AdlsIngestionStore` or `LocalFileIngestionStore`)

### Key Abstractions

**IVendorAdapter** (`src/Canal.Ingestion.ApiLoader/Adapters/IVendorAdapter.cs`): Contract every vendor must implement — URI building, auth headers, response outcome interpretation, pagination sequencing, metadata redaction. Base class `VendorAdapterBase` provides SHA256 request ID generation, JSON parsing, and URI utilities.

**IIngestionStore** (`src/Canal.Ingestion.ApiLoader/Storage/IIngestionStore.cs`): Storage abstraction with two implementations:
- `AdlsIngestionStore` — Azure Blob Storage (production)
- `LocalFileIngestionStore` — Local filesystem for dev without Azure credentials

**EndpointDefinition** (`src/Canal.Ingestion.ApiLoader/Model/EndpointDefinition.cs`): Declarative metadata for an endpoint — resource name, version, HTTP method, page size, watermark support, time spans, `BuildRequests` delegate, `Description`, `DependsOn`.

**EndpointEntry** (`src/Canal.Ingestion.ApiLoader/Model/EndpointEntry.cs`): Pairs a CLI-friendly name with an `EndpointDefinition`. Vendor endpoint catalogs expose `static IReadOnlyList<EndpointEntry> All`.

**Request / FetchResult / FetchMetaData**: Core model chain. `Request` defines what to fetch; `FetchResult` captures the outcome (status, payload bytes, SHA256, pagination info); `FetchMetaData` serializes structured metadata to JSON with snake_case and selective field redaction.

**RequestBuilders** (`src/Canal.Ingestion.ApiLoader/Engine/RequestBuilders.cs`): Factory methods for building request delegates — `Simple()`, `CarrierDependent()`, `CarrierAndTimeWindow()`.

### Adding a New Vendor

1. Create a new adapter project referencing `Canal.Ingestion.ApiLoader`
2. Implement `IVendorAdapter` (or extend `VendorAdapterBase`)
3. Define endpoints as `EndpointDefinition` instances with `Description` and `DependsOn` metadata
4. Add a `static IReadOnlyList<EndpointEntry> All` property to the endpoints class
5. Create a new Exe project referencing `Canal.Ingestion.ApiLoader.Hosting` and the adapter project
6. Write a `Program.cs` (~20-30 lines) using `VendorHostBuilder`
7. Add embedded `hostDefaults.json` with default configuration
   - The host `.csproj` must embed the file via `<EmbeddedResource Include="hostDefaults.json" />`
   - `Program.cs` must load it via `Assembly.GetManifestResourceStream(...)` using the fully-qualified resource name (namespace + filename)

### Storage Path Convention

```
{environment}/{internal|external}/{domain}/{vendor}/{resource}/{version}/{runId}/data_{requestId}_p{pageNr}.json
```

Metadata goes in a parallel `metadata/` subdirectory. Watermarks stored as `ingestion_watermark.json` at the resource/version level.

### Configuration

Configuration is layered (last wins):
1. Embedded `hostDefaults.json` in vendor host assembly
2. `appsettings.json` in working directory (git-ignored)
3. Environment variables
4. CLI arguments (global options like `--environment`, `--storage`)

Settings are bound to:
- `LoaderSettings` — shared loader config (environment, retries, DOP, storage backend)
- `AzureSettings` — ADLS credentials (account, container, tenant, client ID/secret)
- Vendor-specific settings (e.g., `TruckerCloudSettings` for API credentials)

### Retry Logic (FetchEngine)

- 2xx → Success
- 401 → RetryImmediately (refresh auth token)
- 429, 5xx, timeouts → RetryTransient (with delay)
- Other 4xx → FailPermanent
