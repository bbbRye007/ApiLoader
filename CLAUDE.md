# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the entire solution
dotnet build ApiLoader.sln

# Build a specific project
dotnet build src/Canal.Ingestion.ApiLoader/Canal.Ingestion.ApiLoader.csproj

# Run the host console application
dotnet run --project src/Canal.Ingestion.ApiLoader.Host/Canal.Ingestion.ApiLoader.Host.csproj
```

Target framework is .NET 10.0 with nullable reference types and implicit usings enabled. There are no tests in the repository currently.

## Architecture

This is a vendor-agnostic API ingestion engine that fetches data from external APIs and persists it to Azure Data Lake Storage (ADLS) or local filesystem.

### Projects

- **Canal.Ingestion.ApiLoader** — Core library: engine, models, adapters interface, storage interface
- **Canal.Ingestion.ApiLoader.Host** — Console app entry point; wires DI, configures endpoints, runs loads
- **Canal.Ingestion.ApiLoader.TruckerCloud** — Vendor adapter for TruckerCloud API (authenticated, paginated)
- **Canal.Ingestion.ApiLoader.Fmcsa** — Vendor adapter for FMCSA public transportation data API
- **Canal.Storage.Adls** — Azure Blob Storage read/write utilities

### Dependency Graph

```
Host → Core ApiLoader → (references vendor adapters at host level)
TruckerCloud → Core ApiLoader
FMCSA → Core ApiLoader
Core ApiLoader → Canal.Storage.Adls
```

### Execution Pipeline

1. **Program.cs (Host)** — Configures HttpClient, vendor adapter, ingestion store, and endpoint list
2. **EndpointLoaderFactory** → creates **EndpointLoader** per endpoint
3. **EndpointLoader** — Orchestrates a load: manages time windows, watermarks, builds requests via `EndpointDefinition.BuildRequests`
4. **FetchEngine** — Executes HTTP requests with retry logic and configurable parallelism
5. **IVendorAdapter** — Applied at each step for vendor-specific URI construction, auth headers, response interpretation, pagination
6. **IIngestionStore** — Persists payloads and metadata (`AdlsIngestionStore` or `LocalFileIngestionStore`)

### Key Abstractions

**IVendorAdapter** (`src/Canal.Ingestion.ApiLoader/Adapters/IVendorAdapter.cs`): Contract every vendor must implement — URI building, auth headers, response outcome interpretation, pagination sequencing, metadata redaction. Base class `VendorAdapterBase` provides SHA256 request ID generation, JSON parsing, and URI utilities.

**IIngestionStore** (`src/Canal.Ingestion.ApiLoader/Storage/IIngestionStore.cs`): Storage abstraction with two implementations:
- `AdlsIngestionStore` — Azure Blob Storage (production)
- `LocalFileIngestionStore` — Local filesystem for dev without Azure credentials

**Request / FetchResult / FetchMetaData**: Core model chain. `Request` defines what to fetch; `FetchResult` captures the outcome (status, payload bytes, SHA256, pagination info); `FetchMetaData` serializes structured metadata to JSON with snake_case and selective field redaction.

**RequestBuilders** (`src/Canal.Ingestion.ApiLoader/Engine/RequestBuilders.cs`): Factory methods for building request delegates — `Simple()`, `CarrierDependent()`, `CarrierAndTimeWindow()`.

### Adding a New Vendor

1. Create a new project referencing `Canal.Ingestion.ApiLoader`
2. Implement `IVendorAdapter` (or extend `VendorAdapterBase`)
3. Define endpoints as `EndpointDefinition` instances with appropriate `BuildRequests` delegates
4. Wire the adapter and endpoints in `Program.cs`

### Storage Path Convention

```
{environment}/{internal|external}/{domain}/{vendor}/{resource}/{version}/{runId}/data_{requestId}_p{pageNr}.json
```

Metadata goes in a parallel `metadata/` subdirectory. Watermarks stored as `ingestion_watermark.json` at the resource/version level.

### Configuration

Settings come from `appsettings.json` (git-ignored) under `AppSettings:` keys — API credentials, Azure storage account/container/tenant/client. The host reads these via `IConfiguration`.

### Retry Logic (FetchEngine)

- 2xx → Success
- 401 → RetryImmediately (refresh auth token)
- 429, 5xx, timeouts → RetryTransient (with delay)
- Other 4xx → FailPermanent
