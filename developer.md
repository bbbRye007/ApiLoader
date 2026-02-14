# Developer System Prompt — Host Layer Restructuring

You are implementing the design described in `design.md`: replacing the monolithic `Canal.Ingestion.ApiLoader.Host` with a shared hosting library (`Canal.Ingestion.ApiLoader.Hosting`) and thin per-vendor Exe projects (`Host.TruckerCloud`, `Host.Fmcsa`). Work in small, reviewable commits that each leave the solution buildable and preserve output parity.

## Ground Rules

1. **Follow existing patterns first.** Match naming, formatting, namespace conventions, and idioms already in the codebase. Use `sealed record` for immutable data, `sealed class` for mutable config. Use `StringComparison.OrdinalIgnoreCase` for name lookups. Use `ILogger` with structured message templates. Only introduce a new pattern when the design explicitly requires it (e.g., `System.CommandLine`).

2. **Preserve invariants (design.md A-07).** The engine/storage contracts are sacred: do **not** change storage paths, blob naming, watermark JSON format, metadata JSON structure, retry behavior, pagination semantics, or request/response shaping. Avoid modifying engine/storage code (`FetchEngine`, `EndpointLoader`, stores, ADLS*).  
   - If you believe a change to engine/storage is unavoidable to complete the design, **STOP** and:  
     (a) explain why it’s unavoidable,  
     (b) show the smallest possible change, and  
     (c) prove parity with before/after output examples.

3. **Do not modify vendor adapter classes.** `TruckerCloudAdapter` and `FmcsaAdapter` are unchanged. Only `TruckerCloudEndpoints` and `FmcsaEndpoints` get additive metadata changes (new optional properties on existing definitions, new `All` property). If you think an adapter must change, stop and escalate as an open issue.

4. **Additive before destructive.** The old host must keep compiling until the final deletion step. Commits 1–3 are additive-only changes to shared code. The hosting library and vendor hosts are new projects that coexist. Delete the monolith only after live testing confirms parity.

5. **One logical change per commit.** Each commit should be independently reviewable and `dotnet build ApiLoader.sln` must succeed after every commit. Keep diffs tight.

6. **Console discipline.** Do not dump full file bodies to the console. Summarize what changed, list files touched, and show build/test commands + outcomes. If a code snippet is essential, keep it minimal.

## Commit Sequence

This sequence is a collapsed, commit-oriented version of `design.md` §11/§12. Each numbered item below is **one** commit.

### Commit 1 — Core model additions
Add `Description` (string?) and `DependsOn` (string?) to `EndpointDefinition`. Create `EndpointEntry` record in `Model/EndpointEntry.cs`. Both properties are optional with null defaults — zero breaking change.

**Files:**  
- `src/Canal.Ingestion.ApiLoader/Model/EndpointDefinition.cs`  
- `src/Canal.Ingestion.ApiLoader/Model/EndpointEntry.cs`

**Verify:** `dotnet build ApiLoader.sln` (no breaks)

### Commit 2 — TruckerCloud endpoint metadata
Set `Description` and `DependsOn` on each of the 11 `EndpointDefinition` instances in `TruckerCloudEndpoints.cs`. Add `static IReadOnlyList<EndpointEntry> All` property. Use the exact values from `design.md` §7.2.2.

**Files:**  
- `src/Canal.Ingestion.ApiLoader.TruckerCloud/TruckerCloudEndpoints.cs`

**Verify:** `dotnet build` (old host still compiles; it ignores new properties)

### Commit 3 — FMCSA endpoint metadata
Set `Description` on each of the 19 endpoint definitions. Add `static IReadOnlyList<EndpointEntry> All`. Use values from `design.md` §7.2.3.

**Files:**  
- `src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs`

**Verify:** `dotnet build`

### Commit 4 — Hosting project skeleton
Create `Canal.Ingestion.ApiLoader.Hosting` classlib project. Set up `.csproj` with NuGet deps per `design.md` §6.1 (and match the repo’s existing package version management approach). Add project refs to core and `Canal.Storage.Adls`.

Move (copy-then-adjust-namespace; no refactors) the following from the old host into Hosting:
- `LoaderSettings`, `AzureSettings` → `Configuration/`
- `EnvironmentNameSanitizer`, `FlexibleDateParser` → `Helpers/`

Implement `DependencyResolver` per `design.md` §7.1.7. Add Hosting project to `ApiLoader.sln`.

**Files:** New project directory `src/Canal.Ingestion.ApiLoader.Hosting/` with:  
- `Canal.Ingestion.ApiLoader.Hosting.csproj`  
- `Configuration/LoaderSettings.cs`  
- `Configuration/AzureSettings.cs`  
- `Helpers/EnvironmentNameSanitizer.cs`  
- `Helpers/FlexibleDateParser.cs`  
- `DependencyResolver.cs`

**Verify:** `dotnet build` (old host still compiles; it has its own copies)

### Commit 5 — VendorHostBuilder + LoadContext
Implement `VendorHostBuilder` per `design.md` §7.1.2: fluent builder, config loading, settings binding, `System.CommandLine` root command with global options, and the middleware pipeline (CLI overrides, sanitization, logger, cancellation, store, HttpClient, adapter factory, EndpointLoaderFactory). Implement `LoadContext` per §7.1.6.

**Files:**  
- `src/Canal.Ingestion.ApiLoader.Hosting/VendorHostBuilder.cs`  
- `src/Canal.Ingestion.ApiLoader.Hosting/Commands/LoadContext.cs`

**Verify:** `dotnet build`

### Commit 6 — Command builders and handler
Implement `LoadCommandBuilder` (§7.1.3), `LoadCommandHandler` (§7.1.4), and `ListCommandBuilder` (§7.1.5). Follow the conditional option derivation rules from the design exactly. Register `FlexibleDateParser.Parse` for `DateTimeOffset?` parsing on `--start-utc` and `--end-utc` options.

**Files:**  
- `src/Canal.Ingestion.ApiLoader.Hosting/Commands/LoadCommandBuilder.cs`  
- `src/Canal.Ingestion.ApiLoader.Hosting/Commands/LoadCommandHandler.cs`  
- `src/Canal.Ingestion.ApiLoader.Hosting/Commands/ListCommandBuilder.cs`

**Verify:** `dotnet build`

### Commit 7 — TruckerCloud vendor host
Create `Canal.Ingestion.ApiLoader.Host.TruckerCloud` Exe project. Add `Program.cs` (~30 lines), `TruckerCloudSettings.cs`, `hostDefaults.json` (embedded resource), `.csproj`. Add to `ApiLoader.sln`.

**Files:** New project directory `src/Canal.Ingestion.ApiLoader.Host.TruckerCloud/`  
- `.csproj`  
- `Program.cs`  
- `TruckerCloudSettings.cs`  
- `hostDefaults.json` (embedded)

**Verify:** `dotnet build`  
**Smoke:**  
- `apiloader-truckercloud --help`  
- `apiloader-truckercloud list`  
- `apiloader-truckercloud load SafetyEventsV5 --help` (shows conditional options)  
- `apiloader-truckercloud load CarriersV4 --dry-run`

### Commit 8 — FMCSA vendor host
Create `Canal.Ingestion.ApiLoader.Host.Fmcsa` Exe project. Add `Program.cs` (~20 lines), `hostDefaults.json` (embedded), `.csproj`. Add to `ApiLoader.sln`.

**Files:** New project directory `src/Canal.Ingestion.ApiLoader.Host.Fmcsa/`  
- `.csproj`  
- `Program.cs`  
- `hostDefaults.json` (embedded)

**Verify:** `dotnet build`  
**Smoke:**  
- `apiloader-fmcsa list`  
- `apiloader-fmcsa load CompanyCensus --help` (no `--start-utc`/`--end-utc`/`--body-params-json`)  
- `apiloader-fmcsa load CompanyCensus --dry-run`

### Commit 9 — Delete monolithic host
Only after live testing confirms parity: remove `Canal.Ingestion.ApiLoader.Host` from `ApiLoader.sln` and delete `src/Canal.Ingestion.ApiLoader.Host/`.

**Files:**  
- `ApiLoader.sln` modified  
- `src/Canal.Ingestion.ApiLoader.Host/` deleted

**Verify:** `dotnet build ApiLoader.sln` (zero errors). No stale references to the deleted project remain.

### Commit 10 — Update CLAUDE.md
Reflect the new project structure, build commands, and execution instructions.

**Files:**  
- `CLAUDE.md`

## Key Design Decisions to Honor

These are settled (`design.md` §5). Do not revisit or deviate.

- **AD-001:** Use `System.CommandLine` NuGet `2.0.0-beta5.25306.1` for CLI.
- **AD-002:** Separate per-vendor Exe projects, not executable adapter libraries.
- **AD-003:** Delegate-based adapter factory `Func<HttpClient, ILoggerFactory, IVendorAdapter>`. No DI container.
- **AD-004:** Derive CLI options from existing `EndpointDefinition` flags. Add only `Description` and `DependsOn`.
- **AD-005:** One host per vendor, compile-time binding. No runtime discovery.
- **AD-006:** New `Canal.Ingestion.ApiLoader.Hosting` classlib for shared infrastructure.
- **AD-007:** Vendor-owned endpoint catalogs via `All` property. `EndpointRegistry` deleted.
- **AD-008:** Settings split by ownership — shared in Hosting, vendor-specific in vendor host.
- **AD-009:** `TestCommand` not migrated.

## Code Conventions to Match

- **Namespaces:**  
  - `Canal.Ingestion.ApiLoader.Hosting`  
  - `Canal.Ingestion.ApiLoader.Hosting.Commands`  
  - `Canal.Ingestion.ApiLoader.Hosting.Configuration`  
  - `Canal.Ingestion.ApiLoader.Hosting.Helpers`  
  Vendor hosts:  
  - `Canal.Ingestion.ApiLoader.Host.TruckerCloud`  
  - `Canal.Ingestion.ApiLoader.Host.Fmcsa`

- **Access modifiers:** Public API of Hosting library is `VendorHostBuilder` only. Command builders and handler are `internal static`. `LoadContext` is `internal sealed`.

- **Logging:** Use `ILogger` with structured templates: `logger.LogInformation("Message — key={Key}", value)`. Match existing log style.

- **Error handling:** Return exit code `1` from handlers on error. Catch `OperationCanceledException` and return `130`.

- **Null checks:** Use `ArgumentNullException.ThrowIfNull()` for required parameters.

- **Config binding:** Mutable `sealed class` with `{ get; set; }` properties and default values. Bind with `config.GetSection("Name").Bind(target)`.

- **DateTime parsing:** Register `FlexibleDateParser.Parse` as the `System.CommandLine` argument parser for `DateTimeOffset?` to preserve exact parsing behavior.

## What Not To Do

- Do not add tests — the repo has none; live testing validates per `design.md` §11.
- Do not refactor existing code while migrating it. Copy classes to their new homes and adjust only namespace/usings.
- Do not add extra CLI options, validation, or features beyond what the design specifies.
- Do not create abstractions or interfaces for one-off operations.
- Do not introduce `IServiceCollection`/DI container patterns — the design uses factory delegates.
- Do not change storage paths, retry logic, pagination, watermark format, or metadata JSON structure.
