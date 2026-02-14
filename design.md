# Design: Host Layer Restructuring — Auto-Derived, Per-Vendor CLI Architecture

## 1. Problem Summary

The monolithic `Canal.Ingestion.ApiLoader.Host` hard-wires every vendor, endpoint definition, CLI parameter, and help string into a single console application. With 28 endpoints today and 15–20 new vendor adapters expected, this structure forces developers adding a new vendor to learn and modify CLI plumbing instead of focusing on `IVendorAdapter` and `EndpointDefinition` logic. The monolith also prevents independent deployment per vendor, which is required for containerised Kestra orchestration.

This design replaces the monolithic host with:

1. A **shared hosting library** (`Canal.Ingestion.ApiLoader.Hosting`) that owns composition-root responsibilities (configuration layering, storage construction, logging, cancellation, CLI command generation) in a completely vendor-agnostic way.
2. **One thin Exe project per vendor** (~25–35 lines of `Program.cs`) that wires the vendor adapter, endpoint catalog, and vendor-specific settings into the shared host — producing an independently deployable console artifact.
3. **Auto-derived CLI surface** where every endpoint becomes a subcommand of `load`, with options conditionally generated from existing `EndpointDefinition` metadata flags (watermark support → `--start-utc`/`--end-utc`; POST method → `--body-params-json`; paged → `--page-size`; etc.). Help text is generated, not hand-coded.

**Reference**: `requirements.md` §§ 1–9, FR-001 through FR-012, NFR-001 through NFR-006.

---

## 2. Goals and Non-Goals

### Goals (Phase 1 — this design)

| ID | Goal | Traces To |
|---|---|---|
| G-01 | Each vendor produces an independently deployable console Exe artifact | FR-001, AC-004 |
| G-02 | CLI commands, parameters, help text, and validation derived from `EndpointDefinition` metadata — zero hand-coded CLI per endpoint | FR-004, FR-006, FR-007, AC-005 |
| G-03 | `load <endpoint>` command with conditional parameters (time window, page size, watermark, body JSON, dry-run) | FR-005, FR-006 |
| G-04 | Auto-resolved dependency chains (e.g., `load DriversV4` auto-fetches `CarriersV4`) | Existing LoadCommand behaviour preserved |
| G-05 | Vendor #3 developer focuses almost entirely on `IVendorAdapter` + `EndpointDefinition`s; host wiring is ≤ 35 lines of documented boilerplate | FR-008, FR-009, AC-006, AC-007, AC-008 |
| G-06 | Shared infrastructure never modified when a new vendor is added, regardless of its credential shape or constructor requirements | FR-009, FR-012, AC-007 |
| G-07 | Delete the monolithic `Canal.Ingestion.ApiLoader.Host` project | FR-002, AC-003 |
| G-08 | Migrate TruckerCloud (11 endpoints) and FMCSA (19 endpoints) with zero behavioural change | FR-003, NFR-003, NFR-004, AC-001, AC-002 |
| G-09 | `list` command preserved per vendor with compact/verbose modes | Existing ListCommand behaviour preserved |

### Non-Goals (explicitly out of scope)

| ID | Non-Goal | Deferred To |
|---|---|---|
| NG-01 | Structured logging / Application Insights integration | Future work |
| NG-02 | Specific exit codes for Kestra orchestration | Future work |
| NG-03 | JSON output mode for machine parsing | Future work |
| NG-04 | Operational commands: `watermark check`, `watermark reset`, `dry-run` as standalone command | Phase 2 |
| NG-05 | Iteration list I/O between CLI invocations (file path output, stdin JSON input) | Phase 3 |
| NG-06 | CI/CD pipeline, Docker images, Kestra flow definitions | Out of scope |
| NG-07 | Adding any new vendor adapter (but the pattern must support it) | Out of scope |
| NG-08 | `test` command migration (cross-vendor test suite cannot exist in per-vendor hosts; testing is done via `load --max-pages N`) | Not migrated |

---

## 3. Assumptions

| ID | Assumption | Impact if Wrong |
|---|---|---|
| A-01 | The `System.CommandLine` NuGet package (`2.0.0-beta5.25306.1`) is compatible with .NET 10.0 and stable enough for production CLI use. It targets .NET Standard 2.0 and is used by the `dotnet` CLI itself. | If the package is incompatible or unstable, fall back to a thin hand-rolled builder using the existing `CliArgs` pattern with metadata-driven help text generation. The `LoadCommandBuilder` abstraction isolates this choice. |
| A-02 | Every parameter the CLI needs to expose for the `load` command can be derived from the existing boolean flags on `EndpointDefinition` (`SupportsWatermark`, `RequiresIterationList`, `DefaultPageSize`, `HttpMethod`, `MinTimeSpan`, `MaxTimeSpan`). No endpoint requires a truly novel CLI parameter that cannot be inferred from these flags. | If a future endpoint needs a bespoke parameter, the vendor host can add a custom `System.CommandLine` option in its `Program.cs` and pass the value through the adapter factory closure. The architecture does not prevent vendor-level CLI customization — it just doesn't require it for current endpoints. |
| A-03 | The `BuildRequestsDelegate` remains opaque — the CLI does not introspect what parameters a delegate internally consumes. Instead, the CLI uses the boolean flags on `EndpointDefinition` to decide which options to present. This is correct because every current delegate's parameter needs are fully described by the existing flags. | If a new `BuildRequestsDelegate` variant needs parameters beyond what flags describe, a new flag or metadata property can be added to `EndpointDefinition` at that time. |
| A-04 | Dependency chains are single-depth in practice (e.g., DriversV4 → CarriersV4, not DriversV4 → CarriersV4 → SomethingElse). The current `EndpointRegistry.ResolveDependencyChain` handles arbitrary depth with cycle detection, and the design preserves this, but it has only been exercised at depth 1. | No impact — the resolver handles arbitrary depth. |
| A-05 | Each vendor host process executes exactly one command per invocation and then exits. HttpClient lifetime is the process lifetime (short-lived CLI, not a long-running service). This matches the current design. | If a long-running mode is added later, HttpClient management would need to move to `IHttpClientFactory`. This is out of scope. |
| A-06 | The `test` command is a development convenience and is not required in the per-vendor architecture. Per-vendor testing is accomplished via `load` with `--max-pages` and `--storage file`. | If a formal test command is needed, it can be added as a Phase 2 operational command within each vendor host. |
| A-07 | Storage path conventions, watermark JSON format, metadata JSON structure, blob naming, retry behaviour, and pagination logic are immutable invariants. This design does not touch any code in `FetchEngine`, `EndpointLoader`, `IIngestionStore` implementations, `ADLSBlobNamer`, `ADLSWriter`, or `ADLSReader`. | N/A — this is a constraint, not a risk. |

---

## 4. Open Questions

### OQ-001: CLI Framework Selection

**Question**: What mechanism should auto-derive CLI commands, parameters, help text, and validation from endpoint metadata? (requirements.md OQ-001)

**Options evaluated**:

| Option | Pros | Cons |
|---|---|---|
| **A. `System.CommandLine`** | Programmatic API builds commands/options at runtime from metadata. Built-in help generation, type-safe parsing, validation, error messages. Used by `dotnet` CLI itself. NuGet: well-maintained, 19M+ downloads. | Beta label (2.0.0-beta5), though widely deployed in production. Adds ~200KB dependency. |
| **B. Reflection + custom framework** | Zero external deps. Full control. | Reimplements `System.CommandLine` poorly. Significant effort for help text, conditional validation, error messages. |
| **C. Source generators** | Compile-time validation. Zero runtime reflection. | Endpoints are `static readonly` fields — source generators cannot see them. Would require restructuring all endpoint definitions. Extremely high complexity. |
| **D. Keep `CliArgs` + metadata layer** | No new dependency. | Help text, conditional validation, parameter derivation must all be hand-built. Doesn't satisfy FR-004/FR-007 cleanly. |

**Recommended default**: **Option A — `System.CommandLine`**. It is the only option that satisfies FR-004 (auto-derived CLI), FR-006 (conditional parameters), and FR-007 (auto-generated help) without building a framework from scratch. The programmatic API (`new Command(...)`, `new Option<T>(...)`) maps directly to the use case of constructing commands from `EndpointDefinition` metadata at runtime.

---

### OQ-002: Per-Vendor Artifact Structure

**Question**: Should each vendor be a separate `.csproj` console app, or should the vendor class library itself be made executable? (requirements.md OQ-002)

| Option | Pros | Cons |
|---|---|---|
| **A. Separate per-vendor host Exe** | Clean separation (adapter = library, host = app). Independent publish profiles. Adapter library stays reusable. | More projects (2 new Exe projects replace 1 monolith). Each host is a thin ~30-line `Program.cs`. |
| **B. Make adapter library itself executable** | Fewer projects. | Muddies library vs app purpose. Adapter gains host-level NuGet deps (Azure.Identity, System.CommandLine) polluting its surface. Breaks if adapter is ever needed as a library dependency. |

**Recommended default**: **Option A — Separate per-vendor host Exe**. Preserves clean layering. The "cost" of 2 thin Exe projects is exactly the minimal ceremony FR-008 describes.

---

### OQ-003: Vendor-Specific Constructor DI

**Question**: How should vendor-specific constructor dependencies (varying credential shapes, custom HTTP configuration) be registered generically without the shared infrastructure knowing about them? (requirements.md OQ-003 — "the hardest design problem")

| Option | Pros | Cons |
|---|---|---|
| **A. Delegate-based factory** — Shared infra accepts `Func<HttpClient, ILoggerFactory, IVendorAdapter>`. Vendor host captures its own settings in the closure. | Zero reflection. Infinitely extensible (any constructor signature works). Type-safe. Follows existing factory pattern. Infra never sees vendor-specific types. | Each vendor host writes ~5 lines of adapter construction. (This is the intended "minimal wiring".) |
| **B. Full DI container** — `IServiceCollection` with `services.AddSingleton<IVendorAdapter, TruckerCloudAdapter>()`. | Standard .NET pattern. Familiar. | Infra must know how to resolve vendor-specific constructor params, or vendors must register all deps. Overkill for short-lived CLI. Breaks existing manual factory convention. |
| **C. `IVendorBootstrap` interface** — `ConfigureAdapter(IConfiguration, HttpClient, ILoggerFactory)`. | Structured contract. | Interface must anticipate all constructor needs. Vendor #3 needing something beyond these 3 params breaks the interface — violating FR-012. |

**Recommended default**: **Option A — Delegate-based factory**. Only option that truly satisfies FR-012 ("extensible to arbitrary vendor constructor requirements without modifying shared infrastructure"). The delegate captures whatever the vendor needs in its closure.

---

### OQ-004: Endpoint Metadata for CLI Derivation

**Question**: How much metadata needs to be added to `EndpointDefinition` to support CLI derivation? Does this affect existing vendor adapter code? (requirements.md OQ-004)

| Option | Pros | Cons |
|---|---|---|
| **A. Derive from existing boolean flags; add only `Description`** | Zero change to `BuildRequestsDelegate`. Minimal change to `EndpointDefinition` (one optional property). Existing flags are the single source of truth (mitigates RISK-002). | Help text is generic per category, not per-endpoint customised for parameters. |
| **B. Add rich parameter metadata** — e.g., `Parameters = [new ParameterDef("start-utc", ...)]` | Per-endpoint customisation. Self-documenting. | Massive change to all 30 definitions. High drift risk. Duplicates knowledge already in boolean flags. Over-engineered for current needs. |

**Recommended default**: **Option A — Derive from existing flags, add only `Description` (and `DependsOn`) to `EndpointDefinition`**. The existing flags already encode which CLI options apply to each endpoint. Adding `string? Description` and `string? DependsOn` (both optional, null default) is additive and non-breaking.

---

### OQ-005: Reconciling Opaque BuildRequestsDelegate with CLI Introspection

**Question**: `BuildRequestsDelegate` is an opaque delegate. How does the CLI know what parameters an endpoint expects? (requirements.md OQ-005)

**Recommended default**: **Do not introspect the delegate**. Use the boolean flags on `EndpointDefinition` instead. The mapping is deterministic:

| EndpointDefinition flag | CLI parameter derived |
|---|---|
| `SupportsWatermark == true` | `--start-utc`, `--end-utc`, `--no-save-watermark` |
| `DefaultPageSize != null` | `--page-size` (with default shown) |
| `HttpMethod == POST` | `--body-params-json` |
| `RequiresIterationList == true` | *(no CLI param — auto-fetched via `DependsOn`)* |
| *(always)* | `--max-pages`, `--save-behavior`, `--dry-run` |

This resolves OQ-005 without any change to `BuildRequestsDelegate`.

---

### OQ-006: One Host Per Vendor vs. Generic Runtime Discovery

**Question**: Should we build one host per vendor (compile-time binding) or one generic host that discovers adapters at runtime? (requirements.md OQ-006)

| Option | Pros | Cons |
|---|---|---|
| **A. One host per vendor (compile-time)** | Simple. Type-safe. No reflection/scanning. Independent deployment trivial. Build errors caught at compile time. Directly satisfies FR-001. | Adding vendor #3 requires creating a new ~30-line Exe project. |
| **B. One generic host with runtime discovery** | Single deployable. | Contradicts FR-001 ("independently deployable per vendor"). Runtime errors replace compile-time. Assembly loading complexity. Vendor isolation (NFR-002) weakened. |

**Recommended default**: **Option A — One host per vendor**. FR-001 explicitly requires per-vendor independent deployability. Compile-time binding is simpler, safer, and directly satisfies it.

---

## 5. Architecture Decisions

Each decision below is referenced by later sections. Decisions AD-001 through AD-006 correspond to OQ-001 through OQ-006 (see §4 for full option analysis). Decisions AD-007 through AD-009 address structural questions implied by the requirements but not explicitly called out as open questions.

### AD-001: CLI Framework — `System.CommandLine`

- **Context**: FR-004 requires auto-deriving CLI commands, parameters, help, and validation from endpoint metadata. The current host uses a hand-rolled `CliArgs` parser with manually written `HelpText`.
- **Decision**: Use `System.CommandLine` (NuGet `2.0.0-beta5.25306.1`). See OQ-001 for full option analysis.
- **Rationale**: Only option that provides programmatic command/option construction at runtime, built-in help generation, type-safe parsing, and conditional option inclusion — all from `EndpointDefinition` metadata — without building a framework from scratch.
- **Consequences**: Adds `System.CommandLine` NuGet to the hosting library. Deletes `CliArgs`, `HelpText`, `FlexibleDateParser` from the old host. `FlexibleDateParser` is preserved (moved to hosting library) and registered as the custom `System.CommandLine` type converter for `DateTimeOffset` options, so parsing behaviour is identical.
- **Trade-off**: Beta label is accepted. The library is used by the `dotnet` CLI itself and has 19M+ NuGet downloads. If a blocking issue surfaces, the `LoadCommandBuilder` abstraction isolates `System.CommandLine` from the rest of the codebase — a swap to a hand-rolled parser would only affect that one file.

### AD-002: Artifact Structure — Separate Per-Vendor Exe Projects

- **Context**: FR-001 requires independently deployable console artifacts per vendor. OQ-002 asks whether to create separate Exe projects or make adapter libraries executable.
- **Decision**: One new Exe `.csproj` per vendor (e.g., `Canal.Ingestion.ApiLoader.Host.TruckerCloud`). See OQ-002 for full option analysis.
- **Rationale**: Preserves clean separation between adapter logic (classlib, reusable) and host concerns (Exe, deployment unit). Each Exe is ~25–35 lines of documented wiring.
- **Consequences**: Solution gains two new Exe projects; the monolithic `Canal.Ingestion.ApiLoader.Host` is deleted. Each vendor host owns its `Program.cs`, `hostDefaults.json` (embedded resource), and any vendor-specific settings class.
- **Trade-off**: More projects in the solution (net +2 after deleting the monolith). This is the intended "minimal ceremony" per FR-008.

### AD-003: Vendor DI Composition — Delegate-Based Adapter Factory

- **Context**: TruckerCloud needs `(HttpClient, string apiUser, string apiPassword, ILogger<TruckerCloudAdapter>)`; FMCSA needs `(HttpClient, ILogger<FmcsaAdapter>)`; future vendors may need OAuth tokens, client certificates, or novel mechanisms. FR-012 requires the shared infrastructure to accommodate arbitrary constructor signatures without modification. OQ-003 identifies this as "the hardest design problem".
- **Decision**: The hosting library accepts a `Func<HttpClient, ILoggerFactory, IVendorAdapter>` delegate. The vendor host captures its own settings in the closure. See OQ-003 for full option analysis.
- **Rationale**: The delegate is infinitely extensible — any constructor signature works because the vendor host owns the closure. The hosting library never sees vendor-specific types. Type-safe, zero reflection, follows the existing `EndpointLoaderFactory` factory pattern.
- **Consequences**: Each vendor host writes ~5 lines of adapter construction inside the delegate. The hosting library creates the `HttpClient` (5-minute timeout) and provides `ILoggerFactory`; the vendor host adds whatever else it needs.
- **Trade-off**: The delegate signature is fixed at `Func<HttpClient, ILoggerFactory, IVendorAdapter>` — if a future vendor needs the hosting library to provide something beyond HttpClient and ILoggerFactory (unlikely given current patterns), the signature would need to grow. Mitigated by the fact that vendors capture everything else from their own config.

### AD-004: Endpoint Metadata — Derive from Existing Flags

- **Context**: `EndpointDefinition` already carries `SupportsWatermark`, `RequiresIterationList`, `DefaultPageSize`, `HttpMethod`, `MinTimeSpan`, `MaxTimeSpan`. These flags deterministically map to CLI options. OQ-004 and OQ-005 ask how much metadata to add and how to reconcile the opaque `BuildRequestsDelegate`.
- **Decision**: Do not introspect `BuildRequestsDelegate`. Derive CLI options from existing boolean flags. Add two optional properties to `EndpointDefinition`: `string? Description` and `string? DependsOn`. See OQ-004 and OQ-005 for full analysis.
- **Rationale**: The flags are the single source of truth that already describes what each endpoint needs. Adding `Description` enables per-endpoint help text. Moving `DependsOn` from the deleted `EndpointRegistry` into the definition co-locates dependency metadata with the endpoint it describes. Both properties are optional with `null` defaults — zero breaking changes.
- **Consequences**: `EndpointDefinition` grows by 2 optional properties. All 30 existing endpoint definitions in `TruckerCloudEndpoints` and `FmcsaEndpoints` are updated to set `Description` (and `DependsOn` where applicable). No change to `BuildRequestsDelegate`, `IVendorAdapter`, `RequestBuilders`, or any engine code.
- **Trade-off**: Help text is generic per category (e.g., all watermark endpoints show the same `--start-utc` description) rather than per-endpoint customised. This is acceptable for current needs; per-endpoint parameter descriptions can be added later if warranted.

### AD-005: Host Topology — One Host Per Vendor (Compile-Time)

- **Context**: OQ-006 asks whether to use compile-time binding (one Exe per vendor) or runtime discovery (one generic Exe that scans for adapters).
- **Decision**: One host per vendor with compile-time binding. See OQ-006 for full option analysis.
- **Rationale**: FR-001 explicitly requires per-vendor independent deployability. Compile-time binding is simpler, type-safe (build errors caught at compile time), and requires no reflection or assembly scanning.
- **Consequences**: No plugin system. No assembly scanning. The solution has one Exe project per vendor. Adding vendor #3 means creating a new Exe project.
- **Trade-off**: Each new vendor requires a new project (~25–35 lines). This is the intended developer experience, not a burden.

### AD-006: Shared Infrastructure — New `Canal.Ingestion.ApiLoader.Hosting` ClassLib

- **Context**: FR-009/FR-010 require shared CLI infrastructure that handles composition-root responsibilities and doesn't need modification when a new vendor is added. This must live somewhere.
- **Decision**: Create a new `Canal.Ingestion.ApiLoader.Hosting` class library project. It references `Canal.Ingestion.ApiLoader` (core) and `Canal.Storage.Adls`. It takes NuGet dependencies on `System.CommandLine`, `Azure.Identity`, and `Microsoft.Extensions.Configuration.*`/`Logging.*` packages. Vendor Exe projects reference it; vendor adapter libraries do NOT.
- **Rationale**: The alternative — adding hosting logic to the core `Canal.Ingestion.ApiLoader` library — would pollute it with CLI and Azure.Identity dependencies, forcing all consumers (including adapter libraries) to pull in host-level packages. Fundamentally wrong layering.
- **Consequences**: One new classlib project. The core library stays dependency-light (`Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions` only). Vendor adapter libraries remain unaffected.
- **Trade-off**: One more project in the solution. The benefit (clean layering, correct dependency direction) justifies the cost.

### AD-007: Endpoint Registry — Vendor-Owned

- **Context**: The current `EndpointRegistry` in the monolithic host aggregates all endpoints across all vendors with `DependsOn` dependency metadata. With per-vendor hosts, this centralised cross-vendor registry cannot exist.
- **Decision**: Each vendor adapter project exports its own endpoint catalog as a `static IReadOnlyList<EndpointEntry> All` property (e.g., `TruckerCloudEndpoints.All`, `FmcsaEndpoints.All`). The `EndpointEntry` record is defined in the core library. Dependency metadata moves to `EndpointDefinition.DependsOn`. Dependency resolution logic moves to the hosting library as a generic `DependencyResolver` utility that operates on any vendor's endpoint list.
- **Rationale**: Vendor isolation (NFR-002) — each vendor owns its complete endpoint catalog. The hosting library consumes the list generically without knowing vendor specifics. Adding vendor #3 does not touch the hosting library or any other vendor's code (FR-009).
- **Consequences**: `EndpointEntry` record defined in core. `TruckerCloudEndpoints` and `FmcsaEndpoints` each gain a static `All` property. `DependencyResolver` in hosting replaces `EndpointRegistry.ResolveDependencyChain`. The `EndpointRegistry` class is deleted with the monolithic host.
- **Trade-off**: Dependency references (`DependsOn = "CarriersV4"`) are string-based, resolved at runtime within a single vendor's endpoint list. A typo in `DependsOn` produces a runtime error, not a compile-time error. Mitigated by the fact that these strings are static constants that change extremely rarely.

### AD-008: Configuration Settings — Split by Ownership

- **Context**: The monolithic host defines `LoaderSettings`, `TruckerCloudSettings`, and `AzureSettings`. With the host deleted, these need new homes.
- **Decision**:
  - `LoaderSettings` and `AzureSettings` → move to `Canal.Ingestion.ApiLoader.Hosting/Configuration/` (shared across all vendors).
  - `TruckerCloudSettings` → move to `Canal.Ingestion.ApiLoader.Host.TruckerCloud/` (vendor-specific, owned by the vendor host).
  - `EnvironmentNameSanitizer` and `FlexibleDateParser` → move to `Canal.Ingestion.ApiLoader.Hosting/Helpers/` (used by hosting infrastructure).
- **Rationale**: Settings are split by ownership. Shared settings live in the shared library. Vendor-specific settings live in the vendor host. Each vendor host defines its own settings class if needed (FMCSA needs none — public API, no credentials).
- **Consequences**: Namespaces change for moved classes. Functionality is identical. No vendor adapter library is affected.

### AD-009: Test Command — Not Migrated

- **Context**: The current `TestCommand` hard-codes test scenarios for both TruckerCloud and FMCSA in a single cross-vendor command. Per-vendor hosts make this impossible without duplication.
- **Decision**: The `test` command is not migrated. Per-vendor testing is accomplished by invoking the `load` command with `--max-pages N` and `--storage file`. Phase 2 of the requirements roadmap may address operational commands.
- **Rationale**: The test command was a monolith-era development convenience. In the per-vendor model, each endpoint is individually invocable — the same testing scenarios are achieved with `load` plus appropriate flags.
- **Consequences**: `TestCommand` is deleted with the monolithic host. No replacement built. Developers and CI use `load` commands for validation.
- **Trade-off**: Loss of a single-command "run everything" convenience. Acceptable because the per-vendor architecture intentionally isolates vendors.

---

## 6. Project Structure

### 6.1 New Projects

| Project | Type | Path | Purpose | NuGet Dependencies | Project References |
|---|---|---|---|---|---|
| `Canal.Ingestion.ApiLoader.Hosting` | ClassLib | `src/Canal.Ingestion.ApiLoader.Hosting/` | Shared, vendor-agnostic CLI infrastructure: `VendorHostBuilder`, command builders, configuration loading, storage construction, dependency resolution, helpers | `System.CommandLine` 2.0.0-beta5.25306.1, `Azure.Identity` 1.17.1, `Microsoft.Extensions.Configuration` 10.0.2, `Microsoft.Extensions.Configuration.Json` 10.0.2, `Microsoft.Extensions.Configuration.EnvironmentVariables` 10.0.2, `Microsoft.Extensions.Logging.Console` 10.0.2 | `Canal.Ingestion.ApiLoader`, `Canal.Storage.Adls` |
| `Canal.Ingestion.ApiLoader.Host.TruckerCloud` | Exe | `src/Canal.Ingestion.ApiLoader.Host.TruckerCloud/` | TruckerCloud vendor host. Thin `Program.cs` (~30 lines) wiring adapter factory + endpoint list into `VendorHostBuilder`. Owns `TruckerCloudSettings` and embedded `hostDefaults.json`. | *(none — all deps come transitively)* | `Canal.Ingestion.ApiLoader.Hosting`, `Canal.Ingestion.ApiLoader.TruckerCloud` |
| `Canal.Ingestion.ApiLoader.Host.Fmcsa` | Exe | `src/Canal.Ingestion.ApiLoader.Host.Fmcsa/` | FMCSA vendor host. Thin `Program.cs` (~20 lines). No vendor-specific settings (public API). Owns embedded `hostDefaults.json`. | *(none — all deps come transitively)* | `Canal.Ingestion.ApiLoader.Hosting`, `Canal.Ingestion.ApiLoader.Fmcsa` |

**Exe project properties** (both vendor hosts share identical `.csproj` property groups):

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>false</PublishTrimmed>
  <DebugType>embedded</DebugType>
</PropertyGroup>
```

These match the current monolithic host's publish settings exactly.

### 6.2 Modified Projects

| Project | What Changes | Files Affected | Notes |
|---|---|---|---|
| `Canal.Ingestion.ApiLoader` (core) | Add two optional properties to `EndpointDefinition`. Add `EndpointEntry` record. | `Model/EndpointDefinition.cs` (2 lines added), new file `Model/EndpointEntry.cs` (~10 lines) | Additive only. No breaking changes. No new NuGet deps. No change to any other file in the project. |
| `Canal.Ingestion.ApiLoader.TruckerCloud` | Set `Description` and `DependsOn` on each `EndpointDefinition`. Add `static IReadOnlyList<EndpointEntry> All` property. | `TruckerCloudEndpoints.cs` | Additive only. Existing definitions gain optional properties. New `All` property added at bottom of class. |
| `Canal.Ingestion.ApiLoader.Fmcsa` | Set `Description` on each `EndpointDefinition`. Add `static IReadOnlyList<EndpointEntry> All` property. | `FmcsaEndpoints.cs` | Additive only. No `DependsOn` needed (all FMCSA endpoints are simple, no dependencies). |

### 6.3 Deleted Projects

| Project | Path | Replacement | Notes |
|---|---|---|---|
| `Canal.Ingestion.ApiLoader.Host` | `src/Canal.Ingestion.ApiLoader.Host/` | `Canal.Ingestion.ApiLoader.Hosting` (shared lib) + `Host.TruckerCloud` + `Host.Fmcsa` (vendor Exe projects) | Entire directory deleted after migration is verified. Removed from `ApiLoader.sln`. |

**Disposition of every file in the deleted project:**

| File | Disposition | New Location |
|---|---|---|
| `Program.cs` | **Split** | Composition-root logic → `VendorHostBuilder` (hosting). Vendor-specific wiring → per-vendor `Program.cs`. |
| `Configuration/EndpointRegistry.cs` | **Split + deleted** | `EndpointEntry` record → core `Model/EndpointEntry.cs`. Vendor endpoint lists → vendor `*Endpoints.All` properties. `DependsOn` → `EndpointDefinition.DependsOn`. `ResolveDependencyChain` → hosting `DependencyResolver`. The `EndpointRegistry` class itself is deleted. |
| `Configuration/LoaderSettings.cs` | **Moved** | `Hosting/Configuration/LoaderSettings.cs` |
| `Configuration/AzureSettings.cs` | **Moved** | `Hosting/Configuration/AzureSettings.cs` |
| `Configuration/TruckerCloudSettings.cs` | **Moved** | `Host.TruckerCloud/TruckerCloudSettings.cs` |
| `Commands/LoadCommand.cs` | **Replaced** | `Hosting/Commands/LoadCommandBuilder.cs` + `LoadCommandHandler.cs` |
| `Commands/ListCommand.cs` | **Replaced** | `Hosting/Commands/ListCommandBuilder.cs` |
| `Commands/TestCommand.cs` | **Deleted** | Not migrated (AD-009). |
| `Commands/HelpText.cs` | **Deleted** | Auto-generated by `System.CommandLine`. |
| `Helpers/CliArgs.cs` | **Deleted** | Replaced by `System.CommandLine` parsing. |
| `Helpers/EnvironmentNameSanitizer.cs` | **Moved** | `Hosting/Helpers/EnvironmentNameSanitizer.cs` |
| `Helpers/FlexibleDateParser.cs` | **Moved** | `Hosting/Helpers/FlexibleDateParser.cs` |
| `hostDefaults.json` | **Copied per vendor** | Each vendor Exe project gets its own `hostDefaults.json` containing only sections relevant to that vendor. |
| `Canal.Ingestion.ApiLoader.Host.csproj` | **Deleted** | Replaced by `Hosting.csproj` + per-vendor `.csproj` files. |

### 6.4 Dependency Graph

```
┌─────────────────────────────────────────┐   ┌──────────────────────────────────────┐
│ Canal.Ingestion.ApiLoader.Host          │   │ Canal.Ingestion.ApiLoader.Host       │
│   .TruckerCloud  (Exe)                  │   │   .Fmcsa  (Exe)                     │
│                                         │   │                                      │
│ Files:                                  │   │ Files:                               │
│   Program.cs (~30 lines)                │   │   Program.cs (~20 lines)             │
│   TruckerCloudSettings.cs               │   │   hostDefaults.json (embedded)       │
│   hostDefaults.json (embedded)          │   │                                      │
└───────────┬──────────────┬──────────────┘   └──────────┬──────────────┬────────────┘
            │              │                             │              │
    references         references                references         references
            │              │                             │              │
            ▼              ▼                             ▼              ▼
┌───────────────────────────────────┐   ┌──────────────────────────────────────────┐
│ Canal.Ingestion.ApiLoader.Hosting │   │ Canal.Ingestion.ApiLoader.TruckerCloud   │
│ (ClassLib)                        │   │ (ClassLib — UNCHANGED except additive)   │
│                                   │   ├──────────────────────────────────────────┤
│ Files:                            │   │ Canal.Ingestion.ApiLoader.Fmcsa          │
│   VendorHostBuilder.cs            │   │ (ClassLib — UNCHANGED except additive)   │
│   Commands/LoadCommandBuilder.cs  │   └──────────────┬───────────────────────────┘
│   Commands/LoadCommandHandler.cs  │                  │
│   Commands/ListCommandBuilder.cs  │          references (existing)
│   Commands/LoadContext.cs         │                  │
│   DependencyResolver.cs           │                  │
│   Configuration/LoaderSettings.cs │                  ▼
│   Configuration/AzureSettings.cs  │   ┌──────────────────────────────────────────┐
│   Helpers/EnvironmentNameSani...  │   │ Canal.Ingestion.ApiLoader  (ClassLib)    │
│   Helpers/FlexibleDateParser.cs   │   │ (core — UNCHANGED except 2 properties   │
│                                   │   │  on EndpointDefinition + EndpointEntry)  │
│ NuGet: System.CommandLine,        │   │                                          │
│   Azure.Identity,                 │   │ NuGet: Microsoft.Extensions.Http,        │
│   Microsoft.Extensions.*          │   │   Microsoft.Extensions.Logging.Abstr.    │
└───────────┬───────────────────────┘   └──────────────┬───────────────────────────┘
            │                                          │
    references (both)                          references (existing)
            │                                          │
            ▼                                          ▼
┌───────────────────────────────────┐   ┌──────────────────────────────────────────┐
│ Canal.Ingestion.ApiLoader         │   │ Canal.Storage.Adls  (ClassLib)           │
│ (core, same as above ───────────►)│   │ (COMPLETELY UNCHANGED)                   │
└───────────────────────────────────┘   │                                          │
                                        │ NuGet: Azure.Storage.Blobs 12.25.0       │
                                        └──────────────────────────────────────────┘
```

**Key invariant**: Vendor adapter libraries (`TruckerCloud`, `Fmcsa`) do **not** reference the hosting library. Vendor Exe projects reference **both** the hosting library and their respective vendor adapter library. The hosting library is completely vendor-agnostic — it never imports a vendor namespace.

### 6.5 Solution File Changes

The `ApiLoader.sln` will be updated:

- **Remove**: `Canal.Ingestion.ApiLoader.Host` (`{A1B2C3D4-5555-5555-5555-555555555555}`)
- **Add**: `Canal.Ingestion.ApiLoader.Hosting` (new GUID)
- **Add**: `Canal.Ingestion.ApiLoader.Host.TruckerCloud` (new GUID)
- **Add**: `Canal.Ingestion.ApiLoader.Host.Fmcsa` (new GUID)

All new projects nested under the existing `src` solution folder.

---

## 7. Interface & API Design

### 7.1 New Classes / Records

#### 7.1.1 `EndpointEntry` — Core Library

**File**: `src/Canal.Ingestion.ApiLoader/Model/EndpointEntry.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Model;

/// <summary>
/// Associates a CLI-visible name with an <see cref="EndpointDefinition"/>.
/// Vendor adapter projects export a list of these to describe their endpoint catalog.
/// </summary>
/// <param name="Name">
/// The CLI-visible endpoint name (e.g., "CarriersV4", "CompanyCensus").
/// Used as the subcommand name under <c>load</c>. Case-insensitive matching at runtime.
/// </param>
/// <param name="Definition">The endpoint's full definition including metadata flags.</param>
public sealed record EndpointEntry(
    string Name,
    EndpointDefinition Definition);
```

**Design notes**:
- Extracted from the deleted `EndpointRegistry.EndpointEntry` which carried `(Vendor, Name, Definition, DependsOn?, Description?)`. `Vendor` is now implicit (single-vendor host). `DependsOn` and `Description` moved to `EndpointDefinition` itself (AD-004).
- Lives in the core library so both vendor adapter projects (which produce the lists) and the hosting library (which consumes them) can reference it without circular dependencies.

---

#### 7.1.2 `VendorHostBuilder` — Hosting Library

**File**: `src/Canal.Ingestion.ApiLoader.Hosting/VendorHostBuilder.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Hosting;

/// <summary>
/// Fluent builder for constructing and running a vendor-specific CLI host.
/// This is the single public entry point that vendor host Program.cs files use.
/// </summary>
public sealed class VendorHostBuilder
{
    // ── Builder state ──
    private string _vendorDisplayName = string.Empty;
    private string _executableName = string.Empty;
    private Func<HttpClient, ILoggerFactory, IVendorAdapter>? _adapterFactory;
    private IReadOnlyList<EndpointEntry>? _endpoints;
    private Action<IConfigurationBuilder>? _configCallback;
    private readonly List<(string SectionName, object Target)> _vendorSettingsBindings = [];

    /// <summary>Sets the vendor display name used in help text and logging.</summary>
    public VendorHostBuilder WithVendorName(string displayName);

    /// <summary>Sets the executable name shown in CLI help text (e.g., "apiloader-truckercloud").</summary>
    public VendorHostBuilder WithExecutableName(string name);

    /// <summary>
    /// Registers the adapter factory delegate. The hosting library creates the HttpClient
    /// (5-minute timeout, BaseAddress not yet set — adapter constructor sets it) and provides
    /// ILoggerFactory. The vendor host captures its own settings/credentials in the closure.
    /// This is the key extensibility point per AD-003.
    /// </summary>
    public VendorHostBuilder WithAdapterFactory(
        Func<HttpClient, ILoggerFactory, IVendorAdapter> factory);

    /// <summary>Registers the vendor's endpoint catalog.</summary>
    public VendorHostBuilder WithEndpoints(IReadOnlyList<EndpointEntry> endpoints);

    /// <summary>
    /// Optional: adds vendor-specific configuration sources (e.g., an embedded hostDefaults.json).
    /// Called during configuration building after the hosting library adds its own base sources
    /// (external appsettings.json, environment variables).
    /// </summary>
    public VendorHostBuilder ConfigureAppConfiguration(
        Action<IConfigurationBuilder> callback);

    /// <summary>
    /// Optional: registers a vendor-specific settings object to be bound from IConfiguration
    /// during startup. The hosting library calls <c>config.GetSection(sectionName).Bind(target)</c>
    /// before the adapter factory delegate is invoked, so the target object is populated by
    /// the time the delegate runs.
    /// </summary>
    public VendorHostBuilder WithVendorSettings(string sectionName, object target);

    /// <summary>
    /// Builds the System.CommandLine root command tree and invokes it with the provided args.
    /// Returns the process exit code (0 = success, 1 = error, 130 = cancelled).
    /// </summary>
    public async Task<int> RunAsync(string[] args);
}
```

**`RunAsync` implementation outline** (developer reference — not public API):

```
1.  Validate builder state (vendor name, adapter factory, endpoints all required).
2.  Build IConfiguration:
    a. If _configCallback is set, invoke it on the ConfigurationBuilder first
       (this is where the vendor adds its embedded hostDefaults.json stream).
    b. AddJsonFile("appsettings.json", optional: true).
    c. AddEnvironmentVariables().
    d. Build().
3.  Bind shared settings: LoaderSettings from "Loader", AzureSettings from "Azure".
4.  Bind vendor settings: loop _vendorSettingsBindings, call config.GetSection(s).Bind(t).
5.  Build System.CommandLine RootCommand:
    a. Description = "{VendorDisplayName} API Loader".
    b. Add global options: --environment/-e, --storage/-s, --local-storage-path,
       --max-dop, --max-retries.
    c. Add 'load' command via LoadCommandBuilder.Build(endpoints, ...).
    d. Add 'list' command via ListCommandBuilder.Build(endpoints, vendorName).
6.  Add RootCommand middleware (runs before any handler):
    a. Apply global option values as overrides to LoaderSettings.
    b. Sanitize environment name via EnvironmentNameSanitizer.
    c. Create ILoggerFactory (console, from "Logging" config section).
    d. Create CancellationTokenSource with graceful shutdown hooks
       (AssemblyLoadContext.Unloading, ProcessExit, Console.CancelKeyPress).
    e. Build IIngestionStore (file or ADLS based on LoaderSettings.Storage).
    f. Create HttpClient (Timeout = 5 minutes).
    g. Invoke adapter factory delegate → IVendorAdapter.
    h. Create EndpointLoaderFactory(adapter, store, env, maxDop, maxRetries,
       minRetryDelayMs, loggerFactory).
    i. Populate LoadContext and attach to InvocationContext for handler access.
7.  Invoke rootCommand.InvokeAsync(args).
8.  Return exit code.
```

---

#### 7.1.3 `LoadCommandBuilder` — Hosting Library

**File**: `src/Canal.Ingestion.ApiLoader.Hosting/Commands/LoadCommandBuilder.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Builds the <c>load</c> command with one subcommand per endpoint. Each subcommand's
/// options are conditionally derived from the endpoint's <see cref="EndpointDefinition"/> metadata.
/// </summary>
internal static class LoadCommandBuilder
{
    /// <summary>
    /// Creates the <c>load</c> command. For each endpoint in <paramref name="endpoints"/>,
    /// generates a subcommand with conditionally-present options based on the endpoint's
    /// definition metadata (see AD-004 derivation rules).
    /// </summary>
    public static Command Build(IReadOnlyList<EndpointEntry> endpoints);
}
```

**Per-endpoint subcommand construction logic** (the core of FR-004/FR-006):

```
For each EndpointEntry in endpoints:
  1. Create Command(entry.Name, description) where description is built from:
     - Definition.FriendlyName, ResourceVersion, Description
     - "Resource: {ResourceName} | {HttpMethod} | Page size: {DefaultPageSize}"
     - If RequiresIterationList: "Depends on: {DependsOn} (auto-fetched)"
     - If SupportsWatermark: "Watermark: supported | Min: {Min} | Max: {Max}"

  2. Always add:
     - Option<int?>("--max-pages", "Stop after N pages per request")
     - Option<string>("--save-behavior", "PerPage | AfterAll | None") [default: "PerPage"]
     - Option<bool>("--dry-run", "Show execution plan without fetching")

  3. Conditionally add based on EndpointDefinition flags:

     IF DefaultPageSize != null:
       Option<int?>("--page-size", "Override default page size [default: {DefaultPageSize}]")

     IF SupportsWatermark == true:
       Option<DateTimeOffset?>("--start-utc", "Start of time window (default: from watermark)")
       Option<DateTimeOffset?>("--end-utc", "End of time window (default: now)")
       Option<bool>("--no-save-watermark", "Skip saving watermark after load")

     IF HttpMethod == HttpMethod.Post:
       Option<string>("--body-params-json", "JSON body for POST request [default: {}]")

  4. Set handler → invokes LoadCommandHandler.ExecuteAsync with parsed option values.
```

**`DateTimeOffset` option parsing**: Register `FlexibleDateParser.Parse` as a custom `System.CommandLine` `ParseArgument<DateTimeOffset?>` delegate for `--start-utc` and `--end-utc`. This preserves the exact flexible parsing behaviour of the current CLI (ISO 8601, US formats, date-only, etc.).

---

#### 7.1.4 `LoadCommandHandler` — Hosting Library

**File**: `src/Canal.Ingestion.ApiLoader.Hosting/Commands/LoadCommandHandler.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Executes the load for a single endpoint. Resolves dependency chain, auto-fetches
/// dependencies (unsaved), then loads the target endpoint with user-specified parameters.
/// Functionally equivalent to the current <c>LoadCommand.RunAsync</c> but vendor-agnostic.
/// </summary>
internal static class LoadCommandHandler
{
    /// <summary>
    /// Runs the load operation. Called by the System.CommandLine handler for each
    /// endpoint subcommand.
    /// </summary>
    /// <returns>Exit code: 0 = success, 1 = error.</returns>
    public static async Task<int> ExecuteAsync(
        EndpointEntry target,
        IReadOnlyList<EndpointEntry> allEndpoints,
        EndpointLoaderFactory factory,
        ILogger logger,
        CancellationToken cancellationToken,
        int? pageSize,
        int? maxPages,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        SaveBehavior saveBehavior,
        bool saveWatermark,
        string bodyParamsJson,
        bool dryRun);
}
```

**Implementation logic** (mirrors current `LoadCommand.RunAsync`):

```
1. Resolve dependency chain:
   chain = DependencyResolver.Resolve(target, allEndpoints)
   // Returns [dep1, dep2, ..., target] in execution order.
   // Throws InvalidOperationException on circular dependency or missing reference.

2. If dryRun:
   Print execution plan to Console (vendor, endpoint, resource, save behavior,
   watermark, date range, page size, max pages, dependency chain).
   Return 0.

3. Execute chain:
   List<FetchResult>? iterationList = null;
   For i = 0 to chain.Count - 1:
     isTarget = (i == chain.Count - 1)

     If NOT isTarget (dependency step):
       Log "Auto-fetching dependency: {chain[i].Name} (unsaved, for iteration list)"
       iterationList = await factory.Create(chain[i].Definition).Load(
           saveBehavior: SaveBehavior.None,
           saveWatermark: false,
           cancellationToken: cancellationToken)

     If isTarget:
       Log "Loading target endpoint: {target.Name}"
       await factory.Create(target.Definition).Load(
           iterationList: iterationList,
           overrideStartUtc: startUtc,
           overrideEndUtc: endUtc,
           pageSize: pageSize,
           maxPages: maxPages,
           saveBehavior: saveBehavior,
           saveWatermark: saveWatermark,
           bodyParamsJson: bodyParamsJson,
           cancellationToken: cancellationToken)

4. Return 0.
```

---

#### 7.1.5 `ListCommandBuilder` — Hosting Library

**File**: `src/Canal.Ingestion.ApiLoader.Hosting/Commands/ListCommandBuilder.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Builds the <c>list</c> command that displays available endpoints for this vendor.
/// </summary>
internal static class ListCommandBuilder
{
    /// <summary>
    /// Creates the <c>list</c> command with a <c>--verbose</c> flag option.
    /// Compact mode: table of name, version, tags.
    /// Verbose mode: full metadata per endpoint (resource, page size, method,
    /// dependencies, watermark, time span constraints, lookback).
    /// </summary>
    public static Command Build(
        IReadOnlyList<EndpointEntry> endpoints,
        string vendorDisplayName);
}
```

**Implementation notes**: Output format matches the current `ListCommand.Run` exactly — compact table with tags in default mode, full per-endpoint detail in verbose mode. The only difference is that `--vendor` / `-v` filter is removed (vendor is implicit in the per-vendor host).

---

#### 7.1.6 `LoadContext` — Hosting Library

**File**: `src/Canal.Ingestion.ApiLoader.Hosting/Commands/LoadContext.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Runtime context constructed once in <see cref="VendorHostBuilder.RunAsync"/> and
/// made available to command handlers via <c>System.CommandLine.Invocation.InvocationContext</c>.
/// </summary>
internal sealed class LoadContext
{
    public required EndpointLoaderFactory Factory { get; init; }
    public required IReadOnlyList<EndpointEntry> Endpoints { get; init; }
    public required ILogger Logger { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
```

---

#### 7.1.7 `DependencyResolver` — Hosting Library

**File**: `src/Canal.Ingestion.ApiLoader.Hosting/DependencyResolver.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Hosting;

/// <summary>
/// Resolves endpoint dependency chains within a single vendor's endpoint list.
/// Migrated from the deleted <c>EndpointRegistry.ResolveDependencyChain</c>.
/// </summary>
internal static class DependencyResolver
{
    /// <summary>
    /// Returns the dependency chain for <paramref name="target"/> in execution order
    /// (dependencies first, target last). Uses <see cref="EndpointDefinition.DependsOn"/>
    /// to walk the chain.
    /// </summary>
    /// <param name="target">The endpoint the user wants to load.</param>
    /// <param name="allEndpoints">The vendor's full endpoint catalog.</param>
    /// <returns>Ordered list: [deepest dependency, ..., target].</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown on circular dependency or if a referenced <c>DependsOn</c> name is not found
    /// in <paramref name="allEndpoints"/>.
    /// </exception>
    public static List<EndpointEntry> Resolve(
        EndpointEntry target,
        IReadOnlyList<EndpointEntry> allEndpoints)
    {
        var chain = new List<EndpointEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = target;

        while (current is not null)
        {
            if (!visited.Add(current.Name))
                throw new InvalidOperationException(
                    $"Circular dependency detected at '{current.Name}'.");

            chain.Add(current);

            var dependsOn = current.Definition.DependsOn;
            if (dependsOn is null)
                break;

            current = allEndpoints.FirstOrDefault(
                e => e.Name.Equals(dependsOn, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Dependency '{dependsOn}' not found for endpoint '{current.Name}'.");
        }

        chain.Reverse();
        return chain;
    }
}
```

**Design notes**: Logic is functionally identical to `EndpointRegistry.ResolveDependencyChain`. The only change is that `DependsOn` is read from `EndpointDefinition.DependsOn` instead of `EndpointEntry.DependsOn`, and lookup is on `EndpointEntry.Name` within the provided list instead of requiring a `vendor` parameter.

---

#### 7.1.8 `TruckerCloudSettings` — TruckerCloud Host

**File**: `src/Canal.Ingestion.ApiLoader.Host.TruckerCloud/TruckerCloudSettings.cs`

```csharp
namespace Canal.Ingestion.ApiLoader.Host.TruckerCloud;

/// <summary>TruckerCloud vendor-specific settings. Bound from "TruckerCloud" config section.</summary>
public sealed class TruckerCloudSettings
{
    public string ApiUser { get; set; } = string.Empty;
    public string ApiPassword { get; set; } = string.Empty;
}
```

Identical to the current `Host/Configuration/TruckerCloudSettings.cs`. Moved to the vendor host project.

---

### 7.2 Modified Classes

#### 7.2.1 `EndpointDefinition` — Core Library

**File**: `src/Canal.Ingestion.ApiLoader/Model/EndpointDefinition.cs`

**Before:**
```csharp
public sealed record EndpointDefinition
{
    public required string ResourceName { get; init; }
    public required string FriendlyName { get; init; }
    public required int ResourceVersion { get; init; }
    public required BuildRequestsDelegate BuildRequests { get; init; }
    public HttpMethod HttpMethod { get; init; } = HttpMethod.Get;
    public int? DefaultPageSize { get; init; }
    public int DefaultLookbackDays { get; init; } = 90;
    public TimeSpan? MinTimeSpan { get; init; }
    public TimeSpan? MaxTimeSpan { get; init; }
    public bool SupportsWatermark { get; init; } = false;
    public bool RequiresIterationList { get; init; } = false;
}
```

**After** (two properties added at the end):
```csharp
public sealed record EndpointDefinition
{
    public required string ResourceName { get; init; }
    public required string FriendlyName { get; init; }
    public required int ResourceVersion { get; init; }
    public required BuildRequestsDelegate BuildRequests { get; init; }
    public HttpMethod HttpMethod { get; init; } = HttpMethod.Get;
    public int? DefaultPageSize { get; init; }
    public int DefaultLookbackDays { get; init; } = 90;
    public TimeSpan? MinTimeSpan { get; init; }
    public TimeSpan? MaxTimeSpan { get; init; }
    public bool SupportsWatermark { get; init; } = false;
    public bool RequiresIterationList { get; init; } = false;

    /// <summary>Human-readable description shown in CLI help text.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Name of the endpoint this one depends on for its iteration list (e.g., "CarriersV4").
    /// Resolved at runtime within the same vendor's endpoint catalog by
    /// <see cref="Canal.Ingestion.ApiLoader.Hosting.DependencyResolver"/>.
    /// Null means no dependency (simple endpoint).
    /// </summary>
    public string? DependsOn { get; init; }
}
```

**Why**: `Description` enables auto-generated help text per endpoint (FR-007). `DependsOn` moves dependency metadata from the deleted `EndpointRegistry.EndpointEntry` into the definition itself, co-locating it with the endpoint it describes. Both properties are optional with `null` defaults — **zero breaking change** to any existing code that constructs `EndpointDefinition` instances.

---

#### 7.2.2 `TruckerCloudEndpoints` — TruckerCloud Adapter Library

**File**: `src/Canal.Ingestion.ApiLoader.TruckerCloud/TruckerCloudEndpoints.cs`

**Changes**:
1. Each existing `EndpointDefinition` gains `Description` and (where applicable) `DependsOn` init properties.
2. A new `static IReadOnlyList<EndpointEntry> All` property is added.

**Example — DriversV4 before:**
```csharp
public static readonly EndpointDefinition DriversV4 = new()
{
    ResourceName = "drivers",
    FriendlyName = "Drivers",
    ResourceVersion = 4,
    DefaultPageSize = 1000,
    RequiresIterationList = true,
    BuildRequests = RequestBuilders.CarrierDependent(ExtractCarrierCodes),
};
```

**Example — DriversV4 after:**
```csharp
public static readonly EndpointDefinition DriversV4 = new()
{
    ResourceName = "drivers",
    FriendlyName = "Drivers",
    ResourceVersion = 4,
    DefaultPageSize = 1000,
    RequiresIterationList = true,
    BuildRequests = RequestBuilders.CarrierDependent(ExtractCarrierCodes),
    Description = "Drivers per carrier.",
    DependsOn = "CarriersV4",
};
```

**Full `DependsOn` mapping for all TruckerCloud endpoints:**

| Endpoint | DependsOn | Description |
|---|---|---|
| CarriersV4 | `null` | "All carriers. Iteration source for most TC endpoints." |
| VehiclesV4 | `null` | "All vehicles. Iteration source for VehicleIgnitionV4." |
| SubscriptionsV4 | `null` | "All subscriptions." |
| DriversV4 | `"CarriersV4"` | "Drivers per carrier." |
| RiskScoresV4 | `"CarriersV4"` | "Risk scores per carrier." |
| VehicleIgnitionV4 | `"VehiclesV4"` | "Vehicle ignition data. WARNING: very large payloads." |
| SafetyEventsV5 | `"CarriersV4"` | "Safety events per carrier+ELD within a time window." |
| RadiusOfOperationV4 | `"CarriersV4"` | "Radius of operation within a time window." |
| GpsMilesV4 | `"CarriersV4"` | "GPS miles within a time window." |
| ZipCodeMilesV4 | `"CarriersV4"` | "Zip code miles within a time window." |
| TripsV5 | `"CarriersV4"` | "Trip data within a time window (max ~24h)." |

**New `All` property** (added at end of class):
```csharp
/// <summary>All TruckerCloud endpoints as CLI-ready entries.</summary>
public static IReadOnlyList<EndpointEntry> All { get; } =
[
    new("CarriersV4",          CarriersV4),
    new("VehiclesV4",          VehiclesV4),
    new("SubscriptionsV4",     SubscriptionsV4),
    new("DriversV4",           DriversV4),
    new("RiskScoresV4",        RiskScoresV4),
    new("VehicleIgnitionV4",   VehicleIgnitionV4),
    new("SafetyEventsV5",      SafetyEventsV5),
    new("RadiusOfOperationV4", RadiusOfOperationV4),
    new("GpsMilesV4",          GpsMilesV4),
    new("ZipCodeMilesV4",      ZipCodeMilesV4),
    new("TripsV5",             TripsV5),
];
```

---

#### 7.2.3 `FmcsaEndpoints` — FMCSA Adapter Library

**File**: `src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs`

**Changes**: Same pattern as TruckerCloud. Each `EndpointDefinition` gains `Description`. No `DependsOn` needed (all FMCSA endpoints are simple). New `All` property added.

**Full Description mapping:**

| Endpoint | Description |
|---|---|
| InspectionsPerUnit | "Inspections per unit." |
| InsHistAllWithHistory | "Insurance history (all with history)." |
| ActPendInsurAllHistory | "Active/pending insurance history." |
| AuthHistoryAllHistory | "Authority history." |
| Boc3AllHistory | "BOC-3 process agent history." |
| CarrierAllHistory | "Carrier registration history." |
| CompanyCensus | "Company census data." |
| CrashFile | "Crash file data." |
| InsurAllHistory | "Insurance history (all)." |
| InspectionsAndCitations | "Inspections and citations." |
| RejectedAllHistory | "Rejected applications history." |
| RevocationAllHistory | "Revocation history." |
| SpecialStudies | "Special studies data." |
| VehicleInspectionsAndViolations | "Vehicle inspections and violations." |
| VehicleInspectionFile | "Vehicle inspection file." |
| SmsInputMotorCarrierCensus | "SMS input motor carrier census." |
| SmsInputInspection | "SMS input inspection data." |
| SmsInputViolation | "SMS input violation data." |
| SmsInputCrash | "SMS input crash data." |

**New `All` property:**
```csharp
/// <summary>All FMCSA endpoints as CLI-ready entries.</summary>
public static IReadOnlyList<EndpointEntry> All { get; } =
[
    new("InspectionsPerUnit",              InspectionsPerUnit),
    new("InsHistAllWithHistory",           InsHistAllWithHistory),
    new("ActPendInsurAllHistory",          ActPendInsurAllHistory),
    new("AuthHistoryAllHistory",           AuthHistoryAllHistory),
    new("Boc3AllHistory",                  Boc3AllHistory),
    new("CarrierAllHistory",              CarrierAllHistory),
    new("CompanyCensus",                   CompanyCensus),
    new("CrashFile",                       CrashFile),
    new("InsurAllHistory",                InsurAllHistory),
    new("InspectionsAndCitations",        InspectionsAndCitations),
    new("RejectedAllHistory",             RejectedAllHistory),
    new("RevocationAllHistory",           RevocationAllHistory),
    new("SpecialStudies",                 SpecialStudies),
    new("VehicleInspectionsAndViolations", VehicleInspectionsAndViolations),
    new("VehicleInspectionFile",          VehicleInspectionFile),
    new("SmsInputMotorCarrierCensus",     SmsInputMotorCarrierCensus),
    new("SmsInputInspection",             SmsInputInspection),
    new("SmsInputViolation",              SmsInputViolation),
    new("SmsInputCrash",                  SmsInputCrash),
];
```

---

### 7.3 Deleted Classes

All from the deleted `Canal.Ingestion.ApiLoader.Host` project:

| Class | File | Replacement |
|---|---|---|
| `EndpointRegistry` | `Configuration/EndpointRegistry.cs` | Split: `EndpointEntry` (core), vendor `All` properties, `DependencyResolver` (hosting). See §7.1.1, §7.1.7, §7.2.2, §7.2.3. |
| `EndpointRegistry.EndpointEntry` (nested record) | `Configuration/EndpointRegistry.cs` | `Canal.Ingestion.ApiLoader.Model.EndpointEntry` (§7.1.1). Simplified: dropped `Vendor` (implicit) and `DependsOn`/`Description` (moved to `EndpointDefinition`). |
| `LoadCommand` | `Commands/LoadCommand.cs` | `LoadCommandBuilder` (§7.1.3) + `LoadCommandHandler` (§7.1.4). |
| `ListCommand` | `Commands/ListCommand.cs` | `ListCommandBuilder` (§7.1.5). |
| `TestCommand` | `Commands/TestCommand.cs` | Not migrated (AD-009). |
| `HelpText` | `Commands/HelpText.cs` | Auto-generated by `System.CommandLine`. |
| `CliArgs` | `Helpers/CliArgs.cs` | Replaced by `System.CommandLine` parsing. |
| `LoaderSettings` | `Configuration/LoaderSettings.cs` | Moved to `Hosting/Configuration/LoaderSettings.cs`. Identical class, new namespace. |
| `AzureSettings` | `Configuration/AzureSettings.cs` | Moved to `Hosting/Configuration/AzureSettings.cs`. Identical class, new namespace. |
| `TruckerCloudSettings` | `Configuration/TruckerCloudSettings.cs` | Moved to `Host.TruckerCloud/TruckerCloudSettings.cs`. Identical class, new namespace. |
| `EnvironmentNameSanitizer` | `Helpers/EnvironmentNameSanitizer.cs` | Moved to `Hosting/Helpers/EnvironmentNameSanitizer.cs`. Identical class, new namespace. |
| `FlexibleDateParser` | `Helpers/FlexibleDateParser.cs` | Moved to `Hosting/Helpers/FlexibleDateParser.cs`. Identical class, new namespace. |

### 7.4 Unchanged Interfaces / Classes

The following are explicitly **not modified** by this design (NFR-003, NFR-004):

| Abstraction | File | Reason |
|---|---|---|
| `IVendorAdapter` | `Adapters/IVendorAdapter.cs` | No changes needed. The adapter interface is not touched. |
| `VendorAdapterBase` | `Adapters/VendorAdapterBase.cs` | No changes. |
| `TruckerCloudAdapter` | `TruckerCloud/TruckerCloudAdapter.cs` | No changes. Constructor signature, auth logic, pagination all preserved. |
| `FmcsaAdapter` | `Fmcsa/FmcsaAdapter.cs` | No changes. |
| `IIngestionStore` | `Storage/IIngestionStore.cs` | No changes. |
| `AdlsIngestionStore` | `Storage/AdlsIngestionStore.cs` | No changes. |
| `LocalFileIngestionStore` | `Storage/LocalFileIngestionStore.cs` | No changes. |
| `FetchEngine` | `Engine/FetchEngine.cs` | No changes. Retry logic, pagination, parallelism all preserved. |
| `EndpointLoader` | `Client/EndpointLoader.cs` | No changes. Load orchestration (watermark, time window, save behaviour) preserved. |
| `EndpointLoaderFactory` | `Client/EndpointLoaderFactory.cs` | No changes. |
| `RequestBuilders` | `Engine/RequestBuilders.cs` | No changes. `Simple`, `CarrierDependent`, `CarrierAndTimeWindow` all preserved. |
| `Request` | `Model/Request.cs` | No changes. |
| `FetchResult` | `Model/FetchResult.cs` | No changes. |
| `FetchMetaData` | `Model/FetchMetaData.cs` | No changes. |
| `LoadParameters` | `Model/LoadParameters.cs` | No changes. |
| `SaveBehavior` | `Model/SaveBehavior.cs` | No changes. |
| `IngestionRun` | `Model/IngestionRun.cs` | No changes. |
| `IngestionCoordinates` | `Storage/IIngestionStore.cs` | No changes. |
| `JsonQueryHelper` | `Adapters/Utilities/JsonQueryHelper.cs` | No changes. |
| `ADLSAccess` | `Canal.Storage.Adls/ADLSAccess.cs` | No changes. |
| `ADLSBlobNamer` | `Canal.Storage.Adls/ADLSBlobNamer.cs` | No changes. Storage paths unchanged. |
| `ADLSWriter` | `Canal.Storage.Adls/ADLSWriter.cs` | No changes. |
| `ADLSReader` | `Canal.Storage.Adls/ADLSReader.cs` | No changes. |

---

## 8. Configuration Schema

### 8.1 Schema Structure — No Functional Change

The configuration schema (JSON key structure, section names, value types, defaults) is **unchanged** from the current monolithic host. The only structural change is that each vendor host embeds its own `hostDefaults.json` containing only the sections relevant to that vendor, rather than one file containing all vendors' sections.

### 8.2 Configuration Loading Order — Preserved

Precedence (lowest → highest), identical to the current host:

1. **Embedded `hostDefaults.json`** — Baked into the vendor host binary as an embedded resource. Provides base defaults.
2. **External `appsettings.json`** — Optional file next to the exe. Deploy-time overrides without recompiling. Git-ignored.
3. **Environment variables** — Override both (e.g., `Loader__MaxDop=8`, `TruckerCloud__ApiUser=...`).
4. **CLI arguments** — Highest precedence, applied programmatically by `VendorHostBuilder` after configuration binding (e.g., `--environment STAGING` overrides `Loader:Environment`).

### 8.3 Per-Vendor `hostDefaults.json` Files

#### TruckerCloud Host — `src/Canal.Ingestion.ApiLoader.Host.TruckerCloud/hostDefaults.json`

```json
{
  "Loader": {
    "Environment": "UNDEFINED",
    "MaxRetries": 5,
    "MinRetryDelayMs": 100,
    "MaxDop": 8,
    "SaveBehavior": "PerPage",
    "SaveWatermark": true,
    "LookbackDays": 90,
    "Storage": "adls",
    "LocalStoragePath": "C:\\Temp\\ApiLoaderOutput"
  },
  "TruckerCloud": {
    "ApiUser": "",
    "ApiPassword": ""
  },
  "Azure": {
    "AccountName": "",
    "ContainerName": "",
    "TenantId": "",
    "ClientId": "",
    "ClientSecret": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

Identical to the current monolithic `hostDefaults.json`. Contains the `TruckerCloud` section because this vendor needs credentials.

#### FMCSA Host — `src/Canal.Ingestion.ApiLoader.Host.Fmcsa/hostDefaults.json`

```json
{
  "Loader": {
    "Environment": "UNDEFINED",
    "MaxRetries": 5,
    "MinRetryDelayMs": 100,
    "MaxDop": 8,
    "SaveBehavior": "PerPage",
    "SaveWatermark": true,
    "LookbackDays": 90,
    "Storage": "adls",
    "LocalStoragePath": "C:\\Temp\\ApiLoaderOutput"
  },
  "Azure": {
    "AccountName": "",
    "ContainerName": "",
    "TenantId": "",
    "ClientId": "",
    "ClientSecret": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

No `TruckerCloud` section — FMCSA is a public API with no vendor-specific credentials.

### 8.4 Vendor #3 Pattern

A new vendor host creates its own `hostDefaults.json` with the shared `Loader`, `Azure`, and `Logging` sections plus any vendor-specific section (e.g., `"NewVendor": { "ApiKey": "" }`). The vendor host registers the binding via `WithVendorSettings("NewVendor", settings)`. The shared hosting library never parses or validates vendor-specific sections — it only binds them to the object the vendor provides.

---

## 9. CLI Specification

### 9.1 Command Structure

Each vendor host produces a standalone executable with the following command tree. The vendor name is embedded in the executable name — there is no `<vendor>` positional argument (contrast with the current `apiloader load <vendor> <endpoint>`).

```
apiloader-{vendor}
├── load <endpoint> [options]       # Load a single endpoint (dependencies auto-resolved)
├── list [--verbose]                # List available endpoints
└── --help | -h | -?               # Show top-level help
```

**Top-level help** (auto-generated by `System.CommandLine`):

```
Description:
  {VendorDisplayName} API Loader

Usage:
  apiloader-{vendor} [command] [options]

Options:
  --environment, -e <name>     Environment tag for storage path [default: UNDEFINED]
  --storage, -s <type>         Storage backend: adls | file [default: adls]
  --local-storage-path <dir>   Root folder when --storage file [default: C:\Temp\ApiLoaderOutput]
  --max-dop <n>                Max parallel requests [default: 8]
  --max-retries <n>            Max retries per request [default: 5]
  --version                    Show version information
  -?, -h, --help               Show help and usage information

Commands:
  load <endpoint>   Load a single endpoint (dependencies auto-resolved)
  list              List available endpoints
```

### 9.2 Global Options

These apply to all commands and are defined on the `RootCommand`:

| Option | Type | Default | Maps To | Notes |
|---|---|---|---|---|
| `--environment`, `-e` | `string` | `"UNDEFINED"` | `LoaderSettings.Environment` | Sanitised via `EnvironmentNameSanitizer` before use |
| `--storage`, `-s` | `string` | `"adls"` | `LoaderSettings.Storage` | Accepted values: `adls`, `file` |
| `--local-storage-path` | `string` | `"C:\Temp\ApiLoaderOutput"` | `LoaderSettings.LocalStoragePath` | Only relevant when `--storage file` |
| `--max-dop` | `int` | `8` | `LoaderSettings.MaxDop` | |
| `--max-retries` | `int` | `5` | `LoaderSettings.MaxRetries` | |

These override the bound `LoaderSettings` values programmatically (same as the current CLI overrides in `Program.cs` lines 55–69).

### 9.3 Load Command — Per-Endpoint Subcommands

```
Usage:
  apiloader-{vendor} load <endpoint> [options]
```

`<endpoint>` is an argument whose valid values are the names from the vendor's endpoint catalog. `System.CommandLine` provides auto-completion suggestions and validates that the value matches a known endpoint name.

**Load command help** lists all available endpoints:

```
Description:
  Load a single endpoint (dependencies auto-resolved)

Usage:
  apiloader-truckercloud load <endpoint> [options]

Arguments:
  <endpoint>  The endpoint to load. Available:
              CarriersV4, VehiclesV4, SubscriptionsV4, DriversV4, RiskScoresV4,
              VehicleIgnitionV4, SafetyEventsV5, RadiusOfOperationV4, GpsMilesV4,
              ZipCodeMilesV4, TripsV5
```

Each endpoint name is also a subcommand with its own conditionally-generated options. The derivation rules from AD-004 (§5) / §7.1.3 produce the following option sets:

#### Option Derivation Matrix

| Option | Type | Condition | Default | Description |
|---|---|---|---|---|
| `--max-pages` | `int?` | Always | `null` (no limit) | Stop after N pages per request |
| `--save-behavior` | `string` | Always | `"PerPage"` | `PerPage`, `AfterAll`, or `None` |
| `--dry-run` | `bool` (flag) | Always | `false` | Show execution plan without fetching |
| `--page-size` | `int?` | `DefaultPageSize != null` | `null` (uses endpoint default) | Override default page size |
| `--start-utc` | `DateTimeOffset?` | `SupportsWatermark == true` | `null` (from watermark) | Start of time window |
| `--end-utc` | `DateTimeOffset?` | `SupportsWatermark == true` | `null` (now) | End of time window |
| `--no-save-watermark` | `bool` (flag) | `SupportsWatermark == true` | `false` | Skip saving watermark after load |
| `--body-params-json` | `string` | `HttpMethod == POST` | `"{}"` | JSON body for POST request |

#### Concrete Option Sets Per Endpoint Category

**Simple paged** (e.g., CarriersV4, VehiclesV4, SubscriptionsV4, all 19 FMCSA endpoints):
```
Options:
  --max-pages <int>             Stop after N pages per request
  --page-size <int>             Override default page size [default: 1000]
  --save-behavior <behavior>    PerPage | AfterAll | None [default: PerPage]
  --dry-run                     Show execution plan without fetching
```

**Carrier-dependent** (e.g., DriversV4, RiskScoresV4, VehicleIgnitionV4):
```
Options:
  --max-pages <int>             Stop after N pages per request
  --page-size <int>             Override default page size [default: 1000]
  --save-behavior <behavior>    PerPage | AfterAll | None [default: PerPage]
  --dry-run                     Show execution plan without fetching
```
Same as simple paged — no extra options. The dependency (`CarriersV4` / `VehiclesV4`) is auto-fetched; the user does not provide an iteration list via CLI.

**Carrier + time window, GET** (e.g., RadiusOfOperationV4, GpsMilesV4, ZipCodeMilesV4, TripsV5):
```
Options:
  --max-pages <int>             Stop after N pages per request
  --page-size <int>             Override default page size [default: 1000]
  --start-utc <datetime>        Start of time window (default: from watermark)
  --end-utc <datetime>          End of time window (default: now)
  --no-save-watermark           Skip saving watermark after load
  --save-behavior <behavior>    PerPage | AfterAll | None [default: PerPage]
  --dry-run                     Show execution plan without fetching
```

**Carrier + time window, POST** (e.g., SafetyEventsV5):
```
Options:
  --max-pages <int>             Stop after N pages per request
  --page-size <int>             Override default page size [default: 100]
  --start-utc <datetime>        Start of time window (default: from watermark)
  --end-utc <datetime>          End of time window (default: now)
  --no-save-watermark           Skip saving watermark after load
  --body-params-json <json>     JSON body for POST request [default: {}]
  --save-behavior <behavior>    PerPage | AfterAll | None [default: PerPage]
  --dry-run                     Show execution plan without fetching
```

#### Per-Endpoint Help Text (auto-generated example)

```
> apiloader-truckercloud load SafetyEventsV5 --help

Description:
  SafetyEventsV5 (v5) — Safety events per carrier+ELD within a time window.
  Resource: safety-events | POST | Page size: 100
  Depends on: CarriersV4 (auto-fetched)
  Watermark: supported | Min window: 12:00:00

Usage:
  apiloader-truckercloud load SafetyEventsV5 [options]

Options:
  --max-pages <int>             Stop after N pages per request
  --page-size <int>             Override default page size [default: 100]
  --start-utc <datetime>        Start of time window (default: from watermark)
  --end-utc <datetime>          End of time window (default: now)
  --no-save-watermark           Skip saving watermark after load
  --body-params-json <json>     JSON body for POST request [default: {}]
  --save-behavior <behavior>    PerPage | AfterAll | None [default: PerPage]
  --dry-run                     Show execution plan without fetching
  -?, -h, --help                Show help and usage information
```

```
> apiloader-fmcsa load CompanyCensus --help

Description:
  CompanyCensus (v1) — Company census data.
  Resource: az4n-8mr2.json | GET | Page size: 500

Usage:
  apiloader-fmcsa load CompanyCensus [options]

Options:
  --max-pages <int>             Stop after N pages per request
  --page-size <int>             Override default page size [default: 500]
  --save-behavior <behavior>    PerPage | AfterAll | None [default: PerPage]
  --dry-run                     Show execution plan without fetching
  -?, -h, --help                Show help and usage information
```

Note the absence of `--start-utc`, `--end-utc`, `--no-save-watermark`, and `--body-params-json` for `CompanyCensus` — it does not support watermarks and is a GET endpoint. This is FR-006 in action.

### 9.4 List Command

```
Usage:
  apiloader-{vendor} list [options]

Options:
  --verbose     Show detailed endpoint metadata
  -?, -h, --help
```

**Compact output** (default):
```
> apiloader-truckercloud list

TruckerCloud:
--------------------------------------------------------------------------------
  Name                             Ver  Type
  ----                             ---  ----
  CarriersV4                        v4  Simple paged
  VehiclesV4                        v4  Simple paged
  SubscriptionsV4                   v4  Simple paged
  DriversV4                         v4  Requires: CarriersV4
  RiskScoresV4                      v4  Requires: CarriersV4
  VehicleIgnitionV4                 v4  Requires: VehiclesV4
  SafetyEventsV5                    v5  Requires: CarriersV4  Watermark  Time-window
  RadiusOfOperationV4               v4  Requires: CarriersV4  Watermark  Time-window
  GpsMilesV4                        v4  Requires: CarriersV4  Watermark  Time-window
  ZipCodeMilesV4                    v4  Requires: CarriersV4  Watermark  Time-window
  TripsV5                           v5  Requires: CarriersV4  Watermark  Time-window
```

**Verbose output** (`--verbose`): Same per-endpoint detail format as the current `ListCommand` — resource name, page size, HTTP method, description, dependency, watermark support, time span constraints, lookback days.

Output format is functionally identical to the current `ListCommand.Run`. The only difference is removal of the `--vendor` / `-v` filter (vendor is implicit).

### 9.5 Dry-Run Output

```
> apiloader-truckercloud load GpsMilesV4 --dry-run --start-utc 2026-01-01 --end-utc 2026-01-15

=== DRY RUN ===
Endpoint:      GpsMilesV4 (v4)
Resource:      enriched-data/gps-miles
Save behavior: PerPage
Watermark:     save
Start:         2026-01-01T00:00:00.0000000+00:00
End:           2026-01-15T00:00:00.0000000+00:00

Dependency chain (executed in order):
  1. CarriersV4 v4  (fetched, not saved)
  2. GpsMilesV4 v4  <-- target (saved)

No data will be fetched.
```

Format matches the current `LoadCommand` dry-run output exactly.

### 9.6 CLI Examples

```bash
# ── TruckerCloud ──

# Simple endpoint, default settings (ADLS storage)
apiloader-truckercloud load CarriersV4

# Override environment and limit pages
apiloader-truckercloud load DriversV4 --max-pages 3 -e STAGING

# Time-windowed endpoint with explicit date range
apiloader-truckercloud load SafetyEventsV5 --start-utc 2026-01-01 --end-utc 2026-01-15

# POST endpoint with custom body
apiloader-truckercloud load SafetyEventsV5 --body-params-json '{"filter":"active"}'

# Dry run to preview execution plan
apiloader-truckercloud load GpsMilesV4 --dry-run

# Local file storage for development
apiloader-truckercloud load CarriersV4 --storage file --local-storage-path /tmp/output

# Skip watermark save
apiloader-truckercloud load TripsV5 --start-utc 2026-02-13 --end-utc 2026-02-14 --no-save-watermark

# Override page size
apiloader-truckercloud load VehiclesV4 --page-size 500

# List endpoints
apiloader-truckercloud list
apiloader-truckercloud list --verbose

# ── FMCSA ──

# Simple load with page limit
apiloader-fmcsa load CompanyCensus --max-pages 5

# File storage for local testing
apiloader-fmcsa load CrashFile --max-pages 10 --storage file

# Save all results at end instead of per-page
apiloader-fmcsa load InspectionsPerUnit --save-behavior AfterAll --max-pages 3

# List all FMCSA endpoints
apiloader-fmcsa list
```

### 9.7 Exit Codes

| Code | Meaning | Notes |
|---|---|---|
| `0` | Success | Command completed normally |
| `1` | Error | Validation failure, fetch failure, unhandled exception |
| `130` | Cancelled | `Ctrl+C` or process termination (OperationCanceledException) |

Same as the current host. `System.CommandLine` returns `1` for parsing errors (unknown options, missing required arguments) automatically.

---

## 10. Data Flow

### 10.1 Simple Load — FMCSA CompanyCensus

**Command**: `apiloader-fmcsa load CompanyCensus --max-pages 5 --storage file -e TEST`

```
 User shell
   │
   ▼
 Program.cs (Host.Fmcsa)
   │  return await new VendorHostBuilder()
   │      .WithVendorName("FMCSA")
   │      .WithExecutableName("apiloader-fmcsa")
   │      .WithAdapterFactory((http, lf) => new FmcsaAdapter(http, lf.CreateLogger<FmcsaAdapter>()))
   │      .WithEndpoints(FmcsaEndpoints.All)
   │      .ConfigureAppConfiguration(b => b.AddJsonStream(embeddedDefaults))
   │      .RunAsync(args);
   │
   ▼
 VendorHostBuilder.RunAsync(args)
   │
   ├─ 1. Build IConfiguration
   │     embedded hostDefaults.json  →  appsettings.json (optional)  →  env vars
   │
   ├─ 2. Bind settings
   │     LoaderSettings  ← config["Loader"]
   │     AzureSettings   ← config["Azure"]
   │     (no vendor-specific settings for FMCSA)
   │
   ├─ 3. Build System.CommandLine RootCommand
   │     Global options: --environment, --storage, --local-storage-path, --max-dop, --max-retries
   │     'load' command: 19 endpoint subcommands (from FmcsaEndpoints.All)
   │       └─ CompanyCensus subcommand: --max-pages, --page-size, --save-behavior, --dry-run
   │          (no --start-utc/--end-utc — SupportsWatermark is false)
   │          (no --body-params-json — HttpMethod is GET)
   │     'list' command: --verbose
   │
   ├─ 4. System.CommandLine parses args
   │     Matches: load → CompanyCensus
   │     Parsed values: maxPages=5, storage="file", environment="TEST"
   │
   ├─ 5. RootCommand middleware executes
   │     a. Apply CLI overrides: loader.Environment = "TEST", loader.Storage = "file"
   │     b. Sanitize environment: "TEST" → "TEST" (already clean)
   │     c. Create ILoggerFactory (console)
   │     d. Create CancellationTokenSource (Ctrl+C hooks)
   │     e. Build IIngestionStore: storage="file" → new LocalFileIngestionStore("C:\Temp\ApiLoaderOutput")
   │     f. Create HttpClient (Timeout = 5 min)
   │     g. Invoke adapter factory → FmcsaAdapter(httpClient, logger)
   │        HttpClient.BaseAddress set to "https://data.transportation.gov/resource/"
   │     h. Create EndpointLoaderFactory(adapter, store, "TEST", maxDop=8, maxRetries=5, ...)
   │     i. Populate LoadContext { Factory, Endpoints, Logger, CancellationToken }
   │
   ▼
 LoadCommandHandler.ExecuteAsync(target=CompanyCensus, ...)
   │
   ├─ 6. Resolve dependency chain
   │     DependencyResolver.Resolve(CompanyCensus, FmcsaEndpoints.All)
   │     CompanyCensus.Definition.DependsOn = null → chain = [CompanyCensus]
   │
   ├─ 7. No dependencies — skip to target
   │
   ▼
 factory.Create(CompanyCensus.Definition) → EndpointLoader
   │
   ▼
 EndpointLoader.Load(maxPages: 5, saveBehavior: PerPage, ...)
   │
   ├─ 8. InitRun → IngestionRun("TEST", "CarrierInfo", "Fmcsa")
   │     IngestionRunId = "{epochMs}{4-digit-random}"
   │
   ├─ 9. No watermark (SupportsWatermark = false), no iteration list
   │
   ├─ 10. BuildRequests: RequestBuilders.Simple → [1 seed Request]
   │      Request { ResourceName="az4n-8mr2.json", ResourceVersion=1,
   │                HttpMethod=GET, PageSize=500, MaxPages=5 }
   │
   ├─ 11. FetchEngine.ProcessRequest(seedRequest)
   │      │
   │      └─ Pagination loop (adapter-driven):
   │         │
   │         ├─ Step 1: adapter.GetNextRequestAsync(seed, null, 1)
   │         │   → Request with $offset=0, $limit=500, SequenceNr=1
   │         │   → PerformFetch:
   │         │     URI: https://data.transportation.gov/resource/az4n-8mr2.json?$offset=0&$limit=500
   │         │     200 OK → RefineFetchOutcome → Success
   │         │     PostProcessSuccessfulResponse → TotalElements=500, PageNr=1
   │         │   → onPageFetched callback (SaveBehavior.PerPage):
   │         │     store.SaveResultAsync(coords, runId, requestId, pageNr=1, content, metadata)
   │         │     Writes: TEST/external/CarrierInfo/Fmcsa/az4n-8mr2.json/0001/{runId}/data_{hash}_p0001.json
   │         │     Writes: TEST/external/CarrierInfo/Fmcsa/az4n-8mr2.json/0001/{runId}/metadata/metadata_{hash}_p0001.json
   │         │
   │         ├─ Step 2: adapter.GetNextRequestAsync(seed, prevResult, 2)
   │         │   Previous body non-empty → nextOffset=500
   │         │   → Request with $offset=500, $limit=500, SequenceNr=2
   │         │   → PerformFetch → save page 2
   │         │
   │         ├─ Steps 3-5: continue pagination ($offset=1000, 1500, 2000)
   │         │
   │         └─ Step 6: MaxPages=5 reached → loop exits
   │
   ├─ 12. No watermark save (SupportsWatermark = false)
   │
   └─ 13. Return List<FetchResult> (5 results)

 Exit code 0
```

**Storage output** (local filesystem):
```
C:\Temp\ApiLoaderOutput\
  TEST\
    external\
      CarrierInfo\
        Fmcsa\
          az4n-8mr2.json\
            0001\
              {runId}\
                data_{requestId}_p0001.json
                data_{requestId}_p0002.json
                data_{requestId}_p0003.json
                data_{requestId}_p0004.json
                data_{requestId}_p0005.json
                metadata\
                  metadata_{requestId}_p0001.json
                  metadata_{requestId}_p0002.json
                  metadata_{requestId}_p0003.json
                  metadata_{requestId}_p0004.json
                  metadata_{requestId}_p0005.json
```

Path structure is identical to what the current monolithic host produces.

---

### 10.2 Dependent Load — TruckerCloud DriversV4

**Command**: `apiloader-truckercloud load DriversV4 --max-pages 2 -e STAGING --storage file`

```
 User shell
   │
   ▼
 Program.cs (Host.TruckerCloud)
   │  var tcSettings = new TruckerCloudSettings();
   │  return await new VendorHostBuilder()
   │      .WithVendorName("TruckerCloud")
   │      .WithAdapterFactory((http, lf) =>
   │          new TruckerCloudAdapter(http, tcSettings.ApiUser, tcSettings.ApiPassword,
   │                                  lf.CreateLogger<TruckerCloudAdapter>()))
   │      .WithEndpoints(TruckerCloudEndpoints.All)
   │      .WithVendorSettings("TruckerCloud", tcSettings)
   │      .ConfigureAppConfiguration(b => b.AddJsonStream(embeddedDefaults))
   │      .RunAsync(args);
   │
   ▼
 VendorHostBuilder.RunAsync(args)
   │
   ├─ 1. Build IConfiguration (embedded → appsettings.json → env vars)
   │
   ├─ 2. Bind settings
   │     LoaderSettings  ← config["Loader"]
   │     AzureSettings   ← config["Azure"]
   │     TruckerCloudSettings ← config["TruckerCloud"]
   │       tcSettings.ApiUser = "..." (from appsettings or env var)
   │       tcSettings.ApiPassword = "..." (from appsettings or env var)
   │     ┌──────────────────────────────────────────────────────────────────┐
   │     │ tcSettings is now populated. The adapter factory closure         │
   │     │ captures it by reference — when the delegate runs below,        │
   │     │ tcSettings.ApiUser and ApiPassword are already bound.           │
   │     └──────────────────────────────────────────────────────────────────┘
   │
   ├─ 3. Build System.CommandLine RootCommand
   │     'load' command: 11 endpoint subcommands (from TruckerCloudEndpoints.All)
   │       └─ DriversV4 subcommand: --max-pages, --page-size, --save-behavior, --dry-run
   │          (no --start-utc/--end-utc — SupportsWatermark is false)
   │
   ├─ 4. Parse args → load → DriversV4, maxPages=2, environment="STAGING", storage="file"
   │
   ├─ 5. Middleware: apply overrides, build store (LocalFileIngestionStore),
   │     create HttpClient, invoke adapter factory → TruckerCloudAdapter,
   │     create EndpointLoaderFactory
   │
   ▼
 LoadCommandHandler.ExecuteAsync(target=DriversV4, ...)
   │
   ├─ 6. Resolve dependency chain
   │     DependencyResolver.Resolve(DriversV4, TruckerCloudEndpoints.All)
   │     DriversV4.Definition.DependsOn = "CarriersV4"
   │     CarriersV4.Definition.DependsOn = null
   │     → chain = [CarriersV4, DriversV4]
   │
   ├─ 7. Step 1 — Auto-fetch dependency (CarriersV4)
   │     Log: "Auto-fetching dependency: CarriersV4 (unsaved, for iteration list)"
   │     │
   │     ▼
   │   factory.Create(CarriersV4.Definition) → EndpointLoader
   │   EndpointLoader.Load(saveBehavior: None, saveWatermark: false)
   │     │
   │     ├─ BuildRequests: RequestBuilders.Simple → [1 seed Request]
   │     │   Request { ResourceName="carriers", ResourceVersion=4, PageSize=1000 }
   │     │
   │     ├─ FetchEngine.ProcessRequest(seedRequest)
   │     │   │
   │     │   ├─ Step 1: adapter.GetNextRequestAsync → page 1 (?page=1&size=1000)
   │     │   │   → adapter.ApplyRequestHeadersAsync → sets Authorization: Bearer {token}
   │     │   │     (TruckerCloudAdapter fetches auth token via POST /v4/authenticate)
   │     │   │   → 200 OK → PostProcessSuccessfulResponse → TotalPages=3, PageNr=1
   │     │   │
   │     │   ├─ Step 2: page 2 (?page=2&size=1000) → 200 OK
   │     │   │
   │     │   └─ Step 3: page 3 (?page=3&size=1000) → 200 OK → TotalPages reached
   │     │
   │     ├─ SaveBehavior.None → results NOT saved to storage
   │     ├─ saveWatermark: false → watermark NOT saved
   │     │
   │     └─ Return: List<FetchResult> with 3 pages of carrier data
   │        (this becomes the iteration list)
   │
   ├─ 8. Step 2 — Load target (DriversV4)
   │     Log: "Loading target endpoint: DriversV4"
   │     iterationList = [3 FetchResults from CarriersV4]
   │     │
   │     ▼
   │   factory.Create(DriversV4.Definition) → EndpointLoader
   │   EndpointLoader.Load(iterationList: carriers, maxPages: 2, saveBehavior: PerPage)
   │     │
   │     ├─ Validate: RequiresIterationList=true, iterationList is non-null ✓
   │     │
   │     ├─ BuildRequests: RequestBuilders.CarrierDependent(ExtractCarrierCodes)
   │     │   ExtractCarrierCodes(carriers) → extracts carrierCode + codeType from JSON
   │     │   → e.g., 25 carrier rows → 25 seed Requests
   │     │   Each: Request { ResourceName="drivers", queryParameters={carrierCode=X, codeType=DOT} }
   │     │
   │     ├─ Apply maxPages=2 to all 25 seed requests
   │     │
   │     ├─ FetchEngine.ProcessRequests(25 seed requests)
   │     │   Parallel.ForEachAsync (MaxDop=8):
   │     │     For each carrier:
   │     │       Page 1: GET /v4/drivers?carrierCode=X&codeType=DOT&page=1&size=1000
   │     │       Page 2: (if TotalPages > 1) GET /v4/drivers?...&page=2&size=1000
   │     │       MaxPages=2 reached → stop
   │     │     onPageFetched → SaveResultAsync per page
   │     │
   │     ├─ Results saved to:
   │     │   STAGING/external/Telematics/TruckerCloud/drivers/0004/{runId}/data_{hash}_p0001.json
   │     │   STAGING/external/Telematics/TruckerCloud/drivers/0004/{runId}/metadata/metadata_{hash}_p0001.json
   │     │   ... (one pair per carrier per page)
   │     │
   │     └─ No watermark (SupportsWatermark = false)
   │
   └─ 9. Return exit code 0

 Exit code 0
```

**Key behaviours preserved**:
- CarriersV4 auto-fetched but NOT saved (SaveBehavior.None) — identical to current `LoadCommand`
- Auth token fetched once, cached, reused across all 25 carrier requests — existing `TruckerCloudAdapter` behaviour unchanged
- Parallel fan-out at MaxDop=8 — existing `FetchEngine` behaviour unchanged
- Storage paths identical to current output

---

### 10.3 Adding Vendor #3 — Developer Experience Walkthrough

This scenario validates AC-006/AC-007/AC-008: a developer adding a new vendor focuses almost entirely on `IVendorAdapter` + `EndpointDefinition`s.

**Hypothetical vendor**: "Acme" with OAuth2 bearer token auth, 3 endpoints, one of which depends on another.

#### Step 1 — Create adapter library (the real work)

```
src/Canal.Ingestion.ApiLoader.Acme/
├── Canal.Ingestion.ApiLoader.Acme.csproj    (ClassLib, refs Canal.Ingestion.ApiLoader)
├── AcmeAdapter.cs                            (implements IVendorAdapter via VendorAdapterBase)
└── AcmeEndpoints.cs                          (3 EndpointDefinition instances + All property)
```

**AcmeAdapter constructor** (vendor-specific — whatever auth this vendor needs):
```csharp
public sealed class AcmeAdapter : VendorAdapterBase, IVendorAdapter
{
    public AcmeAdapter(HttpClient httpClient, string oauthClientId, string oauthClientSecret,
                       string oauthTokenUrl, ILogger<AcmeAdapter> logger)
        : base(httpClient) { /* ... */ }
}
```

**AcmeEndpoints.cs**:
```csharp
public static class AcmeEndpoints
{
    public static readonly EndpointDefinition Widgets = new()
    {
        ResourceName = "widgets", FriendlyName = "Widgets", ResourceVersion = 1,
        DefaultPageSize = 200, BuildRequests = RequestBuilders.Simple,
        Description = "All widgets.",
    };

    public static readonly EndpointDefinition WidgetDetails = new()
    {
        ResourceName = "widget-details", FriendlyName = "WidgetDetails", ResourceVersion = 1,
        DefaultPageSize = 100, RequiresIterationList = true,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractWidgetIds),
        Description = "Detail records per widget.",
        DependsOn = "Widgets",
    };

    public static readonly EndpointDefinition WidgetMetrics = new()
    {
        ResourceName = "widget-metrics", FriendlyName = "WidgetMetrics", ResourceVersion = 1,
        DefaultPageSize = 500, SupportsWatermark = true,
        MinTimeSpan = TimeSpan.FromHours(1),
        RequiresIterationList = true,
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractWidgetIds, "from", "to"),
        Description = "Time-series metrics per widget.",
        DependsOn = "Widgets",
    };

    public static IReadOnlyList<EndpointEntry> All { get; } =
    [
        new("Widgets", Widgets),
        new("WidgetDetails", WidgetDetails),
        new("WidgetMetrics", WidgetMetrics),
    ];

    private static List<Dictionary<string, string>> ExtractWidgetIds(List<FetchResult> results)
        => /* vendor-specific JSON extraction */ ;
}
```

#### Step 2 — Create vendor host (the minimal wiring, ~30 lines)

```
src/Canal.Ingestion.ApiLoader.Host.Acme/
├── Canal.Ingestion.ApiLoader.Host.Acme.csproj  (Exe, refs Hosting + Acme adapter)
├── Program.cs
├── AcmeSettings.cs
└── hostDefaults.json                            (embedded resource)
```

**Program.cs** (complete):
```csharp
using Canal.Ingestion.ApiLoader.Adapters.Acme;
using Canal.Ingestion.ApiLoader.Hosting;
using System.Reflection;

var acmeSettings = new AcmeSettings();

return await new VendorHostBuilder()
    .WithVendorName("Acme")
    .WithExecutableName("apiloader-acme")
    .WithAdapterFactory((httpClient, loggerFactory) =>
        new AcmeAdapter(
            httpClient,
            acmeSettings.OAuthClientId,
            acmeSettings.OAuthClientSecret,
            acmeSettings.OAuthTokenUrl,
            loggerFactory.CreateLogger<AcmeAdapter>()))
    .WithEndpoints(AcmeEndpoints.All)
    .WithVendorSettings("Acme", acmeSettings)
    .ConfigureAppConfiguration(builder =>
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Canal.Ingestion.ApiLoader.Host.Acme.hostDefaults.json");
        if (stream is not null) builder.AddJsonStream(stream);
    })
    .RunAsync(args);
```

**AcmeSettings.cs**:
```csharp
public sealed class AcmeSettings
{
    public string OAuthClientId { get; set; } = string.Empty;
    public string OAuthClientSecret { get; set; } = string.Empty;
    public string OAuthTokenUrl { get; set; } = string.Empty;
}
```

#### Step 3 — Result (zero changes elsewhere)

```bash
> apiloader-acme load --help
# Shows 3 endpoint subcommands: Widgets, WidgetDetails, WidgetMetrics

> apiloader-acme load WidgetMetrics --help
# Shows: --max-pages, --page-size, --start-utc, --end-utc, --no-save-watermark,
#        --save-behavior, --dry-run
# (auto-derived from SupportsWatermark=true, DefaultPageSize=500)

> apiloader-acme load WidgetDetails --max-pages 2 --storage file -e DEV
# Auto-fetches Widgets (DependsOn), then loads WidgetDetails with iteration list
```

**What the developer did NOT touch**:
- `Canal.Ingestion.ApiLoader` (core) — unchanged
- `Canal.Ingestion.ApiLoader.Hosting` — unchanged
- `Canal.Ingestion.ApiLoader.TruckerCloud` — unchanged
- `Canal.Ingestion.ApiLoader.Fmcsa` — unchanged
- Any CLI plumbing, parameter parsing, help text, command registration — all auto-derived

**What the developer focused on**:
- `AcmeAdapter` — vendor-specific auth, pagination, response handling (the real work)
- `AcmeEndpoints` — 3 endpoint definitions with metadata flags
- `Program.cs` — 25 lines of documented wiring
- `AcmeSettings` — 3 properties

This validates AC-006 ("developer focuses almost entirely on IVendorAdapter + EndpointDefinitions"), AC-007 ("shared infrastructure does not require modification"), and AC-008 ("developer enjoys focusing on vendor specifics").

---

## 11. Migration Plan

### 11.1 Step-by-Step

The migration is designed so that the old monolithic host and the new per-vendor hosts can coexist in the solution until verification is complete. The old host is deleted only as the final step.

| Step | Action | Files Created / Modified | Verification |
|---|---|---|---|
| 1 | **Add `Description` and `DependsOn` to `EndpointDefinition`; add `EndpointEntry` record.** Two optional properties (null default) on an existing record, plus one new 10-line file. | `src/Canal.Ingestion.ApiLoader/Model/EndpointDefinition.cs` (modified, +2 lines). `src/Canal.Ingestion.ApiLoader/Model/EndpointEntry.cs` (new). | `dotnet build ApiLoader.sln` succeeds with zero warnings. No existing code breaks — both properties are optional. |
| 2 | **Update TruckerCloudEndpoints.** Set `Description` and `DependsOn` on each of the 11 `EndpointDefinition` instances. Add `static IReadOnlyList<EndpointEntry> All` property. | `src/Canal.Ingestion.ApiLoader.TruckerCloud/TruckerCloudEndpoints.cs` (modified). | `dotnet build` succeeds. Existing monolithic host still compiles and runs (it ignores the new properties). |
| 3 | **Update FmcsaEndpoints.** Set `Description` on each of the 19 `EndpointDefinition` instances. Add `static IReadOnlyList<EndpointEntry> All` property. | `src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs` (modified). | `dotnet build` succeeds. Old host still works. |
| 4 | **Create `Canal.Ingestion.ApiLoader.Hosting` project.** Scaffold `.csproj` with NuGet deps and project refs. Move `LoaderSettings`, `AzureSettings`, `EnvironmentNameSanitizer`, `FlexibleDateParser` (adjust namespaces). Implement `DependencyResolver`. | New project directory with `.csproj`, `Configuration/LoaderSettings.cs`, `Configuration/AzureSettings.cs`, `Helpers/EnvironmentNameSanitizer.cs`, `Helpers/FlexibleDateParser.cs`, `DependencyResolver.cs`. | `dotnet build` succeeds. Old host still compiles (it has its own copies of the settings/helpers — no conflict). Manually verify `DependencyResolver.Resolve` produces the same chains as `EndpointRegistry.ResolveDependencyChain` for: CarriersV4 (depth 0), DriversV4 (depth 1), VehicleIgnitionV4 (depth 1 via VehiclesV4). |
| 5 | **Implement `VendorHostBuilder`.** Configuration loading, settings binding, `System.CommandLine` root command with global options, middleware pipeline (CLI overrides, sanitisation, logger, cancellation, store, HttpClient, adapter factory, EndpointLoaderFactory). | `VendorHostBuilder.cs`, `Commands/LoadContext.cs`. | `dotnet build` succeeds. Not yet runnable (no vendor host to invoke it). |
| 6 | **Implement `LoadCommandBuilder`, `LoadCommandHandler`, `ListCommandBuilder`.** The command generation and execution logic. | `Commands/LoadCommandBuilder.cs`, `Commands/LoadCommandHandler.cs`, `Commands/ListCommandBuilder.cs`. | `dotnet build` succeeds. |
| 7 | **Create `Canal.Ingestion.ApiLoader.Host.TruckerCloud` project.** `Program.cs`, `TruckerCloudSettings.cs`, `hostDefaults.json` (embedded resource), `.csproj`. Add to `ApiLoader.sln`. | New project directory with 4 files. `ApiLoader.sln` updated. | `dotnet build` succeeds. Smoke tests: (a) `apiloader-truckercloud --help` — top-level help shows global options + `load`/`list` commands. (b) `apiloader-truckercloud list` — prints 11 endpoints with correct names and tags. (c) `apiloader-truckercloud load SafetyEventsV5 --help` — shows conditional options including `--start-utc`, `--end-utc`, `--body-params-json`. (d) `apiloader-truckercloud load CarriersV4 --dry-run` — prints execution plan. |
| 8 | **Live-test TruckerCloud — simple endpoint.** | None (testing only). | `apiloader-truckercloud load CarriersV4 --storage file --max-pages 1 -e MIGTEST`. Verify: data file at `MIGTEST/external/Telematics/TruckerCloud/carriers/0004/{runId}/data_*.json`. Verify metadata JSON structure matches a known-good file from the old host. |
| 9 | **Live-test TruckerCloud — dependent endpoint.** | None. | `apiloader-truckercloud load DriversV4 --storage file --max-pages 1 -e MIGTEST`. Verify: CarriersV4 auto-fetched (no data files for carriers), DriversV4 data saved. |
| 10 | **Live-test TruckerCloud — time-windowed endpoint.** | None. | `apiloader-truckercloud load GpsMilesV4 --start-utc 2026-02-01 --end-utc 2026-02-07 --storage file --max-pages 1 -e MIGTEST`. Verify: data saved, watermark file created at `.../enriched-data__gps-miles/0004/ingestion_watermark.json`. |
| 11 | **Create `Canal.Ingestion.ApiLoader.Host.Fmcsa` project.** `Program.cs`, `hostDefaults.json` (embedded), `.csproj`. Add to `ApiLoader.sln`. | New project directory with 3 files. `ApiLoader.sln` updated. | `dotnet build` succeeds. Smoke tests: (a) `apiloader-fmcsa list` — prints 19 endpoints. (b) `apiloader-fmcsa load CompanyCensus --help` — no `--start-utc`/`--end-utc`/`--body-params-json` (FR-006). (c) `apiloader-fmcsa load CompanyCensus --dry-run`. |
| 12 | **Live-test FMCSA.** | None. | `apiloader-fmcsa load CompanyCensus --storage file --max-pages 2 -e MIGTEST`. Verify data at correct path. Compare metadata JSON structure with known-good output. |
| 13 | **Full regression.** Run all 11 TruckerCloud endpoints (1–2 pages each, `--storage file`) and all 19 FMCSA endpoints (1–2 pages each). | None. | All 30 endpoints produce data files at correct storage paths. No errors. Metadata format unchanged. |
| 14 | **Delete monolithic host.** Remove `Canal.Ingestion.ApiLoader.Host` from `ApiLoader.sln`. Delete `src/Canal.Ingestion.ApiLoader.Host/` directory. | `ApiLoader.sln` modified. Directory deleted. | `dotnet build ApiLoader.sln` succeeds with zero errors. `grep -r "Canal.Ingestion.ApiLoader.Host\"" src/` returns no references to the deleted project (only `Host.TruckerCloud`, `Host.Fmcsa`, and `Hosting`). |
| 15 | **Update `CLAUDE.md`.** Reflect new project structure, build commands, execution instructions. | `CLAUDE.md` (modified). | Reviewed for accuracy against final project layout. |

### 11.2 Coexistence Period

During steps 4–13, both the old monolithic host and the new per-vendor hosts exist in the solution simultaneously. This is intentional:

- The old host continues to compile and run unchanged (steps 1–3 are additive-only changes to shared code).
- The new hosts can be tested in isolation against the same storage backends.
- Side-by-side comparison of output (old vs new) is possible during steps 8–12.
- The old host is deleted only after full regression (step 14).

### 11.3 Deletion Schedule

| Artifact | Deleted At | Prerequisite |
|---|---|---|
| `src/Canal.Ingestion.ApiLoader.Host/` (entire directory) | Step 14 | Full regression (step 13) passes |
| Old host entry in `ApiLoader.sln` | Step 14 | Same |

No gradual deprecation — the old host is deleted in one step after verification. There is no intermediate state where the old host is "soft-deprecated" but still referenced.

---

## 12. Implementation Order

| Order | Component | Dependencies | Est. Complexity | Verification | Est. Effort |
|---|---|---|---|---|---|
| 1 | `EndpointDefinition` +2 properties, `EndpointEntry` record (core) | None | Low | `dotnet build` — zero breaks | ~15 min |
| 2 | `TruckerCloudEndpoints`: add `Description`/`DependsOn`, add `All` | Step 1 | Low | `dotnet build` | ~30 min |
| 3 | `FmcsaEndpoints`: add `Description`, add `All` | Step 1 | Low | `dotnet build` | ~20 min |
| 4 | `Hosting` project skeleton: `.csproj`, settings classes, helpers (moved), `DependencyResolver` | Step 1 | Medium | `dotnet build`; verify `DependencyResolver` chains | ~1 hr |
| 5 | `VendorHostBuilder`: config loading, settings binding, middleware, `System.CommandLine` root command with global options | Step 4 | High | `dotnet build` | ~2 hr |
| 6 | `LoadCommandBuilder` + `LoadCommandHandler` + `ListCommandBuilder` | Steps 4–5 | High | `dotnet build` | ~2 hr |
| 7 | `Host.TruckerCloud` project: `Program.cs`, settings, defaults, `.csproj` | Steps 2, 5, 6 | Low | `list`, `--help`, `--dry-run` smoke tests | ~30 min |
| 8 | `Host.Fmcsa` project: `Program.cs`, defaults, `.csproj` | Steps 3, 5, 6 | Low | `list`, `--help`, `--dry-run` smoke tests | ~20 min |
| 9 | Live testing: TruckerCloud (simple, dependent, time-windowed) | Step 7 | Medium | Data at correct paths, metadata matches | ~1 hr |
| 10 | Live testing: FMCSA (simple paged) | Step 8 | Low | Data at correct paths, metadata matches | ~30 min |
| 11 | Full regression: all 30 endpoints | Steps 9–10 | Medium | All endpoints pass | ~1 hr |
| 12 | Delete old host, update `.sln`, update `CLAUDE.md` | Step 11 | Low | `dotnet build` clean; no stale refs | ~15 min |

**Critical path**: 1 → 4 → 5 → 6 → 7 → 9 → 11 → 12

**Parallelisable work**:
- Steps 2 and 3 (endpoint updates) can run in parallel with step 4 (hosting skeleton).
- Steps 7 and 8 (vendor hosts) can run in parallel after step 6.
- Steps 9 and 10 (live testing) can run in parallel.

**Total estimated effort**: ~10 hours of implementation + testing.

---

## 13. Risks & Mitigations

| ID | Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|---|
| R-01 | **`System.CommandLine` beta incompatibility with .NET 10.** The package targets .NET Standard 2.0, which should be compatible, but .NET 10 is new. | Medium | Low | Verify compatibility in step 4 (hosting skeleton). If incompatible, check for a newer nightly build. Worst case: replace `LoadCommandBuilder` with a hand-rolled metadata-driven parser using the moved `CliArgs` class — the `LoadCommandHandler` and rest of the architecture are unaffected. |
| R-02 | **Storage path regression.** A bug in the new host could produce different storage paths than the old host. | High | Low | No storage path code is modified (`LocalFileIngestionStore`, `AdlsIngestionStore`, `ADLSBlobNamer` are all unchanged). Risk exists only if adapter or EndpointDefinition metadata is passed differently. Mitigated by side-by-side comparison during migration steps 8–12: run old host and new host for the same endpoint and diff the output paths. |
| R-03 | **`DependsOn` string typo produces runtime error.** Dependency references are strings resolved at runtime. A typo in a `DependsOn` value would fail at invocation time, not compile time. | Medium | Low | The `DependsOn` values are static constants that change extremely rarely. Currently only 8 of 30 endpoints have `DependsOn` set, all referencing either `"CarriersV4"` or `"VehiclesV4"`. Verified at migration step 9 (dependent endpoint live test). |
| R-04 | **Endpoint parameter derivation misses an edge case (RISK-001 from requirements).** A future endpoint may need a CLI parameter that cannot be inferred from existing `EndpointDefinition` boolean flags. | Medium | Low | Current endpoints are fully covered. If a future endpoint needs a bespoke parameter: (a) add a new flag to `EndpointDefinition` and update `LoadCommandBuilder` derivation rules, or (b) the vendor host can add a custom `System.CommandLine` option in its `Program.cs` and pass the value via the adapter factory closure or `bodyParamsJson`. The architecture supports both escape hatches. |
| R-05 | **Description/DependsOn metadata drift from actual behaviour (RISK-002 from requirements).** `Description` is display-only. `DependsOn` replaces the existing `EndpointRegistry` dependency metadata — it is not a new parallel declaration but a relocation of existing data. | Low | Low | `DependsOn` is the single source of truth for dependency resolution (it replaces `EndpointEntry.DependsOn` in the deleted registry). `Description` affects only help text. Neither drives runtime fetch logic. |
| R-06 | **`FlexibleDateParser` behaviour change under `System.CommandLine`.** If `System.CommandLine` applies its own `DateTimeOffset` parsing before our custom parser, dates may be interpreted differently. | Medium | Low | Register `FlexibleDateParser.Parse` as a `ParseArgument<DateTimeOffset?>` delegate on the `--start-utc` and `--end-utc` options. This bypasses `System.CommandLine`'s default type converter entirely. Verify during step 9 with explicit date values (ISO 8601, US format, date-only). |
| R-07 | **Vendor host `Program.cs` wiring errors for vendor #3.** A developer unfamiliar with the pattern may struggle with the ~30-line `Program.cs`. | Low | Medium | Provide a documented template or example in `CLAUDE.md`. Both existing vendor hosts serve as copy-paste references. The `VendorHostBuilder` API is fluent and discoverable with XML doc comments. |
| R-08 | **Increased solution complexity.** Net +2 projects (3 new − 1 deleted). Developers must understand which project to modify for which purpose. | Low | Medium | Clear project naming convention (`Host.*` = vendor Exe, `Hosting` = shared lib, no `Host` suffix = adapter lib). Updated `CLAUDE.md` documents the project structure. |

---

## 14. Acceptance Criteria Verification

| AC | Requirement | How This Design Satisfies It | Verified At |
|---|---|---|---|
| AC-001 | TruckerCloud (all endpoints) invocable individually with endpoint-appropriate parameters; data lands correctly in storage. | Each of the 11 TruckerCloud endpoints is a subcommand of `apiloader-truckercloud load`. Options are conditionally derived (§9.3). `LoadCommandHandler` orchestrates via `EndpointLoader.Load` (unchanged). Storage paths unchanged (§7.4). | Migration steps 8–10, 13 |
| AC-002 | FMCSA (all endpoints) invocable individually with endpoint-appropriate parameters; data lands correctly in storage. | Each of the 19 FMCSA endpoints is a subcommand of `apiloader-fmcsa load`. Same derivation and execution logic. | Migration steps 12–13 |
| AC-003 | Monolithic `Canal.Ingestion.ApiLoader.Host` deleted with no loss of functionality. | Deleted at migration step 14 after full regression. All functionality migrated: `load` → `LoadCommandHandler`, `list` → `ListCommandBuilder`, composition root → `VendorHostBuilder`, endpoint registry → vendor `All` exports + `DependencyResolver`. `test` not migrated (AD-009 — replaced by `load --max-pages N`). | Migration step 14 |
| AC-004 | Each vendor produces an independently deployable console artifact. | `Host.TruckerCloud` and `Host.Fmcsa` are separate Exe projects (AD-002) with `PublishSingleFile`/`SelfContained` (§6.1). Each produces a standalone executable. | `dotnet publish` each project independently |
| AC-005 | CLI help text auto-generated and accurately reflects each endpoint's supported parameters (required vs optional, conditional on capabilities). | `LoadCommandBuilder` derives options from `EndpointDefinition` flags (AD-004, §7.1.3, §9.3). `System.CommandLine` renders help text including defaults and descriptions. Conditional options absent for endpoints that don't support them (demonstrated in §9.3 examples). | Migration steps 7, 11 (`--help` smoke tests) |
| AC-006 | Developer adding vendor #3 focuses almost entirely on `IVendorAdapter` + `EndpointDefinition`s. Host/CLI layer requires minimal, well-documented steps — no hand-coded parameter parsing, help text, or command registration. | Vendor #3 walkthrough (§10.3): developer creates adapter + endpoints + ~25-line `Program.cs`. Zero CLI plumbing. `VendorHostBuilder` handles everything. | §10.3 walkthrough; both existing vendor hosts demonstrate the pattern |
| AC-007 | Shared CLI infrastructure does not require modification to accommodate vendor #3's configuration needs, even if different from TruckerCloud and FMCSA. | Delegate-based adapter factory (AD-003): `Func<HttpClient, ILoggerFactory, IVendorAdapter>`. The closure captures arbitrary vendor-specific deps. `WithVendorSettings` binds any config section to any object. Hosting library never imports vendor namespaces. | §10.3 walkthrough (OAuth2 vendor with 3 constructor params not in TruckerCloud/FMCSA) |
| AC-008 | Litmus test: developer adding a new vendor should enjoy focusing on vendor specifics. If they get dragged into CLI plumbing, that is a failure. | The vendor host `Program.cs` is pure wiring — vendor name, adapter factory, endpoint list, settings. No parameter parsing, no help text, no command registration, no option validation. All of that is derived automatically by the hosting library from `EndpointDefinition` metadata. | Subjective; §10.3 demonstrates the experience end-to-end |
