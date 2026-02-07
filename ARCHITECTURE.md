# Canal Ingestion Platform -- Architecture

## Overview

The Canal ingestion platform follows a **Core + Vendor Adapters** architecture. A single core framework defines all contracts, shared mechanics (fetch engine, retry logic, pagination orchestration, storage, metadata), while each vendor lives in its own isolated package that implements those contracts and contains _only_ vendor-specific concerns.

```
┌─────────────────────────────────────────────────────────────────┐
│  HOST APPLICATION                                               │
│  Owns: credentials, HttpClient, cancellation, orchestration     │
│  References: Core + whichever vendor packages it needs          │
└────────┬──────────────────────────────┬─────────────────────────┘
         │                              │
         ▼                              ▼
┌─────────────────────┐   ┌──────────────────────────────────────┐
│  Vendor Package(s)  │   │  Canal.Ingestion.ApiLoader (Core)    │
│  TruckerCloud       │──▶│  IVendorAdapter, VendorAdapterBase   │
│  Fmcsa              │   │  FetchEngine, RequestBuilders        │
│  YourNextVendor     │   │  EndpointLoader, EndpointDefinition  │
│  ...                │   │  Model types, FetchMetaData          │
└─────────────────────┘   └────────────────┬─────────────────────┘
                                           │
                                           ▼
                          ┌────────────────────────────────────┐
                          │  Canal.Storage.Adls                │
                          │  Blob read/write/naming (standalone)│
                          └────────────────────────────────────┘
```

---

## NuGet Package Strategy

### Package Map

| Package | Contents | Dependencies |
|---------|----------|--------------|
| `Canal.Storage.Adls` | ADLSWriter, ADLSReader, ADLSBlobNamer, ADLSAccess | Azure.Storage.Blobs, Azure.Identity |
| `Canal.Ingestion.ApiLoader` | IVendorAdapter, VendorAdapterBase, FetchEngine, RequestBuilders, EndpointLoader, EndpointLoaderFactory, all Model types, FetchMetaData, JsonQueryHelper | Canal.Storage.Adls |
| `Canal.Ingestion.ApiLoader.Vendors.TruckerCloud` | TruckerCloudAdapter, TruckerCloudEndpoints, internal extractors | Canal.Ingestion.ApiLoader |
| `Canal.Ingestion.ApiLoader.Vendors.Fmcsa` | FmcsaAdapter, FmcsaEndpoints | Canal.Ingestion.ApiLoader |

### Dependency Graph

```
Canal.Storage.Adls                              (standalone -- no framework dependency)
         ▲
Canal.Ingestion.ApiLoader                       (core -- references Storage)
         ▲
Canal.Ingestion.ApiLoader.Vendors.TruckerCloud  (vendor -- references Core only)
Canal.Ingestion.ApiLoader.Vendors.Fmcsa         (vendor -- references Core only)
         ▲
Host Application                                (references Core + vendor packages it needs)
```

### Key Properties

- **Vendors never reference each other.** TruckerCloud knows nothing about FMCSA and vice versa.
- **Core never references vendors.** Adding a vendor is purely additive -- zero changes to core.
- **Storage is standalone.** It can be used independently of the ingestion framework.
- **Host picks its vendors.** A TruckerCloud-only host doesn't pull in FMCSA dependencies.

### Versioning Strategy

All packages should follow SemVer. Core and Storage should version independently of vendors. Vendor packages pin a minimum compatible Core version via their `<ProjectReference>` (or `<PackageReference>` once published to a NuGet feed).

A breaking change to `IVendorAdapter` bumps Core's major version and requires vendor packages to update. Non-breaking additions (new optional interface members with defaults, new model properties) are minor bumps.

---

## Solution Structure

```
ApiLoader/
├── Canal.sln
├── Directory.Build.props                            # Shared: TFM, nullable, implicit usings
│
├── src/
│   ├── Canal.Storage.Adls/                          # Standalone storage package
│   │   ├── Canal.Storage.Adls.csproj
│   │   ├── ADLSAccess.cs
│   │   ├── ADLSWriter.cs
│   │   ├── ADLSReader.cs
│   │   └── ADLSBlobNamer.cs
│   │
│   ├── Canal.Ingestion.ApiLoader/                   # Core framework package
│   │   ├── Canal.Ingestion.ApiLoader.csproj
│   │   ├── Adapters/
│   │   │   ├── IVendorAdapter.cs                    # THE contract
│   │   │   ├── VendorAdapterBase.cs                 # Shared plumbing (ID computation, JSON helpers)
│   │   │   └── Utilities/
│   │   │       └── JsonQueryHelper.cs               # JSON path extraction for vendor extractors
│   │   ├── Engine/
│   │   │   ├── FetchEngine.cs                       # Vendor-agnostic fetch execution
│   │   │   └── RequestBuilders.cs                   # Simple, CarrierDependent, CarrierAndTimeWindow
│   │   ├── Client/
│   │   │   ├── EndpointLoader.cs                    # Load() orchestration
│   │   │   ├── EndpointLoaderBase.cs                # Storage/watermark plumbing
│   │   │   └── EndpointLoaderFactory.cs             # Factory for creating loaders
│   │   └── Model/
│   │       ├── EndpointDefinition.cs                # Endpoint config + BuildRequests delegate
│   │       ├── Request.cs                           # Immutable request representation
│   │       ├── FetchResult.cs                       # Result with timing, status, content
│   │       ├── FetchFailure.cs                      # Per-attempt failure record
│   │       ├── FetchMetaData.cs                     # Rich metadata sidecar builder
│   │       ├── IngestionRun.cs                      # Run identity (ID, domain, vendor, timestamp)
│   │       ├── LoadParameters.cs                    # Parameters for Load() calls
│   │       ├── PaginatedRequestSettings.cs          # Vendor-neutral pagination intent
│   │       └── SaveBehavior.cs                      # AfterAll / PerPage / None
│   │
│   ├── Canal.Ingestion.ApiLoader.Vendors.TruckerCloud/
│   │   ├── Canal.Ingestion.ApiLoader.Vendors.TruckerCloud.csproj
│   │   ├── TruckerCloudAdapter.cs                   # Auth, paging, response interpretation
│   │   ├── TruckerCloudEndpoints.cs                 # Endpoint catalog (static definitions)
│   │   └── Endpoints/Internal/                      # Vendor-specific data extractors
│   │       ├── List_CarrierCodes.cs
│   │       ├── List_CarrierCodeAndEldVendors.cs
│   │       └── List_VehiclesCarrierCodesAndEldVendor.cs
│   │
│   └── Canal.Ingestion.ApiLoader.Vendors.Fmcsa/
│       ├── Canal.Ingestion.ApiLoader.Vendors.Fmcsa.csproj
│       ├── FmcsaAdapter.cs                          # Socrata paging, response interpretation
│       └── FmcsaEndpoints.cs                        # 19 FMCSA dataset endpoints
│
└── examples/
    └── Canal.Ingestion.ApiLoader.ExampleHost/       # Reference host application
        ├── Canal.Ingestion.ApiLoader.ExampleHost.csproj
        └── Program.cs
```

---

## The Contract: IVendorAdapter

Every vendor adapter implements `IVendorAdapter`. This is the seam between vendor-specific behavior and the vendor-agnostic engine.

| Method | Purpose | Who calls it |
|--------|---------|-------------|
| `BuildRequestUri` | Construct the full URI for a request | FetchEngine |
| `ApplyRequestHeadersAsync` | Set auth headers, content-type, etc. | FetchEngine |
| `RefineFetchOutcome` | Override generic HTTP status interpretation with vendor knowledge | FetchEngine |
| `PostProcessSuccessfulResponse` | Extract pagination counters, element counts from response body | FetchEngine |
| `GetNextRequestAsync` | Decide the next request in a paging chain (or null to stop) | FetchEngine |
| `BuildMetaDataJson` | Serialize metadata with vendor-specific redaction rules | FetchResult |
| `BuildFailureMessage` | Human-readable failure description with vendor context | FetchEngine |
| `ComputeRequestId/AttemptId/PageId` | Deterministic identity for blob naming and deduplication | FetchEngine, storage |
| `ResourceNameFriendly` | Map opaque resource IDs to human-readable names | Request |

`VendorAdapterBase` provides default implementations for identity computation, query string building, JSON parsing helpers, and header application. Vendors extend it and focus on their specific behavior.

---

## What Core Owns vs. What Vendors Own

### Core Owns (vendor-agnostic)
- HTTP execution with retry policy (transient/permanent/immediate classification)
- Parallel request dispatch (`Parallel.ForEachAsync` with configurable MaxDOP)
- Paging orchestration loop (delegates "what's next?" to the adapter)
- Storage: blob naming, payload+metadata writes, watermark read/write
- Request identity (SHA-256 canonical form, excluding auth/paging noise)
- Metadata structure (standardized across all vendors)
- `RequestBuilders` -- reusable patterns (Simple, CarrierDependent, CarrierAndTimeWindow)

### Vendors Own (vendor-specific)
- Authentication mechanics (token caching, refresh-on-401, no-auth, OAuth, etc.)
- URL construction (versioned paths, query param conventions)
- Pagination semantics (page-based, offset-based, cursor-based, etc.)
- Response body interpretation (vendor timeout markers, empty-body semantics)
- Endpoint catalog (static `EndpointDefinition` instances)
- Data extractors (JSON path mappings for dependent endpoints)
- Metadata redaction rules (which headers/params contain secrets)

---

## Adding a New Vendor

1. Create a new project: `src/Canal.Ingestion.ApiLoader.Vendors.{VendorName}/`
2. Add a `.csproj` referencing `Canal.Ingestion.ApiLoader`
3. Implement a class extending `VendorAdapterBase`:
   - Override the abstract members: `IngestionDomain`, `VendorName`, `BaseUrl`, `IsExternalSource`
   - Override `BuildRequestUri`, `RefineFetchOutcome`, `PostProcessSuccessfulResponse`, `GetNextRequestAsync`, `BuildFailureMessage`
   - Optionally override `ApplyRequestHeadersAsync` (for auth), `BuildMetaDataJson` (for redaction), `ResourceNameFriendly`
4. Create a static endpoints catalog class with `EndpointDefinition` fields
5. Add the project to `Canal.sln`

**No changes to core or other vendor packages required.**

For common patterns, use the built-in `RequestBuilders`:
- `RequestBuilders.Simple` -- single request, no dependencies
- `RequestBuilders.CarrierDependent(extractFn)` -- one request per extracted row
- `RequestBuilders.CarrierAndTimeWindow(extractFn)` -- above + time window params

For unusual endpoints, write a custom `BuildRequestsDelegate` inline -- the engine doesn't care how requests are built.

---

## Known Extension Points for Future Vendors

| Scenario | Current State | What to Do |
|----------|--------------|------------|
| Cursor/continuation-token pagination | `PaginatedRequestSettings` supports `StartIndex` + `RequestSize`; adapters handle cursor state in `GetNextRequestAsync` | Works today -- adapter manages cursor internally |
| POST-body filters instead of query params | `Request.BodyParamsJson` exists but is basic | Extend as needed; FetchEngine already sends body for POST |
| OAuth2 / API-key auth | TruckerCloud uses username/password token; FMCSA uses none | Implement in adapter's `ApplyRequestHeadersAsync` |
| Rate limiting (429 backoff) | FetchEngine retries with flat `MinRetryDelayMs` on transient | Could add exponential backoff or Retry-After header parsing |
| Streaming large responses | `SaveBehavior.PerPage` saves each page as it arrives | Could add `HttpCompletionOption.ResponseContentRead` streaming |
| Non-JSON payloads (CSV, XML) | `ADLSWriter.SavePayloadAndMetadata` has a byte[] overload | Adapter can skip JSON parsing in `PostProcess` |

---

## Design Decisions

### Why separate Storage from Core?
`Canal.Storage.Adls` has zero dependency on the ingestion framework. Teams that need blob read/write/naming without the fetch engine can reference it alone. It also allows swapping storage implementations (e.g., local filesystem for testing) without touching core.

### Why `VendorAdapterBase` instead of just `IVendorAdapter`?
Request identity computation (SHA-256 canonical form) and query string building are identical across all vendors. Duplicating that in every adapter would be error-prone. The base class provides these shared mechanics while keeping vendor-specific behavior in abstract/virtual overrides.

### Why delegates for `BuildRequests` instead of a class hierarchy?
The original design used per-endpoint classes. The delegate approach (`BuildRequestsDelegate`) is more flexible -- simple endpoints use `RequestBuilders.Simple`, complex ones inline a lambda, and there's no class proliferation. The endpoint catalog becomes a flat list of record definitions.

### Why are adapters `public sealed` instead of `internal`?
Host applications need to instantiate vendor adapters directly (they pass in `HttpClient`, credentials, etc.). The adapter must be visible to the host. `sealed` because vendor adapters are leaf implementations -- there's no reason to subclass `TruckerCloudAdapter`.

### Why `public` for `VendorAdapterBase`, `FetchMetaData`, `JsonQueryHelper`?
These live in the Core package but are consumed by vendor packages:
- Vendor adapters extend `VendorAdapterBase`
- Vendor adapters use `FetchMetaData` in `BuildMetaDataJson` overrides
- Vendor extractors use `JsonQueryHelper.QuickQuery` to pull data from JSON responses
