# Design: Vendor-Agnostic Core Engine Refactor

Active docs used: ActiveRequirements: @requirements.md, ActiveDesign: @design.md, ActiveDecisions: @decisions.md

## 1. Problem Statement

The core engine project (`Canal.Ingestion.ApiLoader`) contains vendor-specific "Carrier" semantics in method names, comments, and an error message string. These must be replaced with vendor-agnostic terminology so that new adapter authors encounter only generic engine primitives.

## 2. Scope

### In Scope
- Rename `CarrierDependent` and `CarrierAndTimeWindow` methods in `RequestBuilders.cs`
- Update all XML doc / inline comments in the core project to remove vendor-specific language from example text
- Update the error message string in `EndpointLoader.cs`
- Update all vendor adapter call sites to use new method names
- Update one vendor-specific comment in the Hosting project
- Bootstrap `CLAUDE.md` and create `decisions.md`

### Out of Scope
- No new features or capabilities
- No changes to `FetchEngine.cs` logic, retry behavior, or pagination mechanics
- No changes to `IVendorAdapter` or `VendorAdapterBase` interface contracts (method signatures unchanged)
- No changes to `Model/` types
- No changes to `Storage/` layer
- No structural changes to the adapter pattern
- No test creation (no test projects exist)

## 3. Requirements

### Functional (FR-###)
- FR-001: `CarrierDependent` and `CarrierAndTimeWindow` renamed to vendor-agnostic names
- FR-002: All code identifiers in core project free of vendor-specific terms
- FR-003: `EndpointLoader.cs` error message uses vendor-agnostic language
- FR-004: Comments may reference vendors only as proof of engine generality
- FR-005: All vendor adapter call sites updated in same change
- FR-006: Sensible defaults (ISO-8601 time format) preserved

### Non-Functional (NFR-###)
- NFR-001: New adapter authors encounter only generic engine concepts
- NFR-002: Renamed methods are self-documenting (describe the iteration pattern)
- NFR-003: Solution compiles cleanly with zero errors and zero new warnings

## 4. Constraints
- .NET 10.0; no new dependencies
- No `[Obsolete]` wrappers or backward-compatible shims (DEC-002: clean break)
- No signature changes — only names and comments (DEC-003)
- No class restructuring of `RequestBuilders`

## 5. Invariants (Must Not Change)

### 5.1 Invariant Policy (from decisions.md)
- DEC-003: Delegate signatures, record shapes, and interface contracts are hard invariants
- DEC-002: Breaking name changes are allowed (pre-production)

### 5.2 Hard Invariants (external + durable)
- INV-001: `BuildRequestsDelegate` delegate signature: `(IVendorAdapter, EndpointDefinition, int?, LoadParameters) → List<Request>`
- INV-002: `EndpointDefinition` record — all property names and types unchanged
- INV-003: `IVendorAdapter` interface — all method signatures unchanged
- INV-004: `RequestBuilders.Simple` method — name and signature unchanged (already vendor-agnostic)
- INV-005: `RequestBuilders.CarrierDependent` / `CarrierAndTimeWindow` — parameter types and return types unchanged; only method names change
- INV-006: Default parameter values: `startParamName = "startTime"`, `endParamName = "endTime"`, `timeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'"` — preserved as-is

### 5.3 Soft/Contextual Invariants
- INV-101: `LoadParameters.IterationList` naming — not changed (already generic)
- INV-102: `EndpointDefinition.RequiresIterationList` naming — not changed (already generic)
- INV-103: Vendor adapter internal helpers (extractor methods in `TruckerCloudEndpoints.cs`) — not renamed (they live in the vendor project where vendor terms are appropriate)

## 6. Open Questions

- OQ-001: What should the new method names be? — **Resolved.** See AD-001 below.

## 7. Architecture Decisions (ADRs)

### AD-001: Naming for `CarrierDependent` and `CarrierAndTimeWindow`

- **Context:** The two factory methods on `RequestBuilders` need vendor-agnostic names that describe the *iteration pattern* they implement, not any vendor's domain entity. The existing codebase already uses "IterationList" in `LoadParameters` and `EndpointDefinition.RequiresIterationList`.

- **Options:**
  1) `PerRow` / `PerRowWithTimeWindow` — Concise; describes the core mechanic ("one request per row from the iteration list"). Reads naturally at call sites: `RequestBuilders.PerRow(extractFn)`.
  2) `IterationBased` / `IterationBasedWithTimeWindow` — Aligns with existing `IterationList` terminology. Slightly more abstract; doesn't convey the 1:1 row→request mapping as clearly.
  3) `RowDependent` / `RowDependentWithTimeWindow` — Parallels the old `CarrierDependent` structure but with generic term. Slightly redundant ("dependent" is implied by the factory pattern).
  4) `DependentEndpoint` / `DependentEndpointWithTimeWindow` — Focuses on the endpoint's dependency relationship rather than the request-building pattern. Less descriptive of what the method actually does.

- **Decision:** Option 1 — `PerRow` / `PerRowWithTimeWindow`

- **Rationale:**
  - Most concise and self-documenting: immediately communicates "one request per row"
  - Reads naturally at call sites: `RequestBuilders.PerRow(ExtractCarrierCodes)` clearly says "build one request per row, using this extractor"
  - The `extractRows` parameter name already uses "rows" terminology — `PerRow` is perfectly aligned
  - Shortest names reduce visual noise in endpoint definitions which are already dense
  - NFR-002 satisfied: a developer reading `RequestBuilders.PerRow(...)` understands the pattern without knowing any vendor's domain model

- **Consequences / trade-offs:**
  - Slightly less aligned with `IterationList` naming than Option 2, but "row" is more concrete and descriptive
  - All 8 call sites in `TruckerCloudEndpoints.cs` must be updated (3× `PerRow`, 5× `PerRowWithTimeWindow`)
  - Zero call sites in `FmcsaEndpoints.cs` affected (all use `Simple`)

- **Decision record impact:** No — this is a one-time rename decision, not a durable policy. DEC-001 (vendor-agnostic policy) already covers the "why."

## 8. Current State Summary

### Core Engine (`Canal.Ingestion.ApiLoader`)
The `RequestBuilders` static class provides three factory methods that create `BuildRequestsDelegate` instances:
- `Simple` — single request, no iteration list needed (already vendor-agnostic ✓)
- `CarrierDependent` — one request per row extracted from prior results (**vendor-specific name**)
- `CarrierAndTimeWindow` — one request per row with time window parameters added (**vendor-specific name**)

Vendor-specific contamination points (audit from requirements.md §Audit Inventory):

| File | Location | Issue |
|---|---|---|
| `Engine/RequestBuilders.cs:20` | Method name | `CarrierDependent` |
| `Engine/RequestBuilders.cs:34` | Method name | `CarrierAndTimeWindow` |
| `Engine/RequestBuilders.cs:9` | Comment | "Carriers, Vehicles, all FMCSA, etc" |
| `Engine/RequestBuilders.cs:18` | Comment | "Drivers, RiskScores where each carrier gets its own request" |
| `Engine/RequestBuilders.cs:32` | Comment | "SafetyEvents, GpsMiles, RadiusOfOperation, ZipCodeMiles" |
| `Adapters/IVendorAdapter.cs:8` | Comment | `// ie "Telematics"` |
| `Adapters/IVendorAdapter.cs:9` | Comment | `// ie "TruckerCloud"` |
| `Adapters/IVendorAdapter.cs:10` | Comment | `// ie "https://api.truckercloud.com/api/"` |
| `Adapters/IVendorAdapter.cs:12` | Comment | `// ie "true" because Trucker Cloud Apis...` |
| `Client/EndpointLoader.cs:36` | String literal | `"carrier results from a prior Load() call"` |
| `Hosting/VendorHostBuilder.cs:62` | Comment | `"e.g., an embedded truckerCloudDefaults.json"` |

### Call Sites (vendor adapter projects)
- `TruckerCloudEndpoints.cs`: 3× `CarrierDependent`, 5× `CarrierAndTimeWindow`
- `FmcsaEndpoints.cs`: 0 references (all use `Simple`)

## 9. Proposed Design

### 9.1 Component Overview

```
┌─────────────────────────────────────────────┐
│  Canal.Ingestion.ApiLoader (core engine)    │
│                                             │
│  Engine/RequestBuilders.cs                  │
│    Simple(...)           ← unchanged        │
│    PerRow(...)           ← renamed from     │
│                            CarrierDependent │
│    PerRowWithTimeWindow  ← renamed from     │
│         (...)              CarrierAndTime   │
│                            Window           │
│                                             │
│  Adapters/IVendorAdapter.cs                 │
│    (comments updated only)                  │
│                                             │
│  Client/EndpointLoader.cs                   │
│    (error message string updated)           │
├─────────────────────────────────────────────┤
│  Canal.Ingestion.ApiLoader.TruckerCloud     │
│    TruckerCloudEndpoints.cs                 │
│      3× PerRow, 5× PerRowWithTimeWindow     │
├─────────────────────────────────────────────┤
│  Canal.Ingestion.ApiLoader.Fmcsa            │
│    (no changes — all use Simple)            │
├─────────────────────────────────────────────┤
│  Canal.Ingestion.ApiLoader.Hosting          │
│    VendorHostBuilder.cs                     │
│      (one comment updated)                  │
└─────────────────────────────────────────────┘
```

### 9.2 Detailed Changes (file-by-file)

#### `src/Canal.Ingestion.ApiLoader/Engine/RequestBuilders.cs`
- **Change type:** Modify
- **Responsibility:** Rename two methods; update three comment blocks
- **Changes:**
  1. Rename method `CarrierDependent` → `PerRow` (line 20)
  2. Rename method `CarrierAndTimeWindow` → `PerRowWithTimeWindow` (line 34)
  3. Update `Simple` XML doc comment (line 9):
     - FROM: `"Single request, no prior data needed. Used by simple paged endpoints (Carriers, Vehicles, all FMCSA, etc)."`
     - TO: `"Single request, no prior data needed. Used by simple paged endpoints that don't require an iteration list."`
  4. Update `PerRow` XML doc comment (lines 17–18):
     - FROM: `"One request per row extracted from prior results. The extractRows function returns query-parameter dictionaries.\n/// Used by endpoints like Drivers, RiskScores where each carrier gets its own request."`
     - TO: `"One request per row extracted from prior results. The extractRows function returns query-parameter dictionaries,\n/// each producing one seed request."`
  5. Update `PerRowWithTimeWindow` XML doc comment (lines 31–32):
     - FROM: `"One request per row extracted from prior results, with time window parameters added to each request.\n/// Used by endpoints like SafetyEvents, GpsMiles, RadiusOfOperation, ZipCodeMiles."`
     - TO: `"One request per row extracted from prior results, with time window parameters added to each request."`
- **Public API:** `PerRow(Func<List<FetchResult>, List<Dictionary<string, string>>>)` and `PerRowWithTimeWindow(Func<...>, string, string, string)` — same signatures, new names
- **DI/config impacts:** None
- **Verification:** Compile succeeds; grep for old names returns zero hits in core project

#### `src/Canal.Ingestion.ApiLoader/Adapters/IVendorAdapter.cs`
- **Change type:** Modify
- **Responsibility:** Update inline comments to use generic examples or frame vendor mentions as proof of flexibility
- **Changes:**
  1. Line 8: `// ie "Telematics"` → `// e.g., "Telematics", "Compliance" — the business domain this adapter serves`
  2. Line 9: `// ie "TruckerCloud"` → `// e.g., "TruckerCloud", "Fmcsa" — identifies the vendor for storage paths and logging`
  3. Line 10: `// ie "https://api.truckercloud.com/api/"` → `// vendor API root URL, e.g., "https://api.example.com/v1/"`
  4. Line 12: `// ie "true" because Trucker Cloud Apis are cconsidered "an external source of data"` → `// true when data originates outside the organization (most vendor APIs)`
- **Public API:** Unchanged — comments only
- **Verification:** No signature changes; compile succeeds

#### `src/Canal.Ingestion.ApiLoader/Client/EndpointLoader.cs`
- **Change type:** Modify
- **Responsibility:** Update error message string to remove "carrier" reference
- **Changes:**
  1. Line 36: `"e.g., carrier results from a prior Load() call"` → `"e.g., results from a prior Load() call that this endpoint depends on"`
- **Public API:** Unchanged — string literal only
- **Verification:** Compile succeeds; grep for "carrier" in EndpointLoader.cs returns zero hits

#### `src/Canal.Ingestion.ApiLoader.TruckerCloud/TruckerCloudEndpoints.cs`
- **Change type:** Modify
- **Responsibility:** Update call sites from old method names to new names
- **Changes:**
  1. Line 57: `RequestBuilders.CarrierDependent(ExtractCarrierCodes)` → `RequestBuilders.PerRow(ExtractCarrierCodes)`
  2. Line 71: `RequestBuilders.CarrierDependent(ExtractCarrierCodes)` → `RequestBuilders.PerRow(ExtractCarrierCodes)`
  3. Line 86: `RequestBuilders.CarrierDependent(ExtractVehicleData)` → `RequestBuilders.PerRow(ExtractVehicleData)`
  4. Line 105: `RequestBuilders.CarrierAndTimeWindow(...)` → `RequestBuilders.PerRowWithTimeWindow(...)`
  5. Line 122: `RequestBuilders.CarrierAndTimeWindow(...)` → `RequestBuilders.PerRowWithTimeWindow(...)`
  6. Line 139: `RequestBuilders.CarrierAndTimeWindow(...)` → `RequestBuilders.PerRowWithTimeWindow(...)`
  7. Line 156: `RequestBuilders.CarrierAndTimeWindow(...)` → `RequestBuilders.PerRowWithTimeWindow(...)`
  8. Line 174: `RequestBuilders.CarrierAndTimeWindow(...)` → `RequestBuilders.PerRowWithTimeWindow(...)`
- **Public API:** No public API changes (these are static field initializers)
- **Verification:** Compile succeeds

#### `src/Canal.Ingestion.ApiLoader.Hosting/VendorHostBuilder.cs`
- **Change type:** Modify
- **Responsibility:** Update one comment to use generic example
- **Changes:**
  1. Line 62: `"e.g., an embedded truckerCloudDefaults.json"` → `"e.g., an embedded vendorDefaults.json"`
- **Public API:** Unchanged — comment only
- **Verification:** Compile succeeds

## 10. Configuration Schema

N/A — no configuration changes in this refactor.

## 11. Observability & Operations

N/A — no changes to logging, metrics, or error handling behavior. Log messages that reference vendor names do so via `VendorAdapter.VendorName` property (runtime value, not hardcoded), which is correct and unchanged.

## 12. Migration Plan

- **Step-by-step:** Single atomic change. Rename methods and update all call sites in one commit. No dual-run, no staged rollout.
- **Rollback notes:** Revert the commit. No data migration involved.
- **Data migration/backfill:** N/A — pure code refactor.

## 13. Planned Commits (Developer-ready)

Each commit must be independently buildable.

### Commit 0 — Bootstrap CLAUDE.md and decisions.md
- **Changes:**
  - Create `CLAUDE.md` from template with repo-specific content (build commands, project inventory, safety constraints)
  - Create `decisions.md` with DEC-001 (vendor-agnostic policy), DEC-002 (breaking changes allowed), DEC-003 (invariant policy)
- **Verification:**
  - Files exist at repo root
  - Developer verifies build command: `dotnet build ApiLoader.sln` succeeds
  - Developer updates verification stamp in `CLAUDE.md` after confirming
- **Invariants preserved:** No code changes; repo contract only

### Commit 1 — Rename RequestBuilders methods and update core project comments
- **Changes:**
  - `Engine/RequestBuilders.cs`: Rename `CarrierDependent` → `PerRow`, `CarrierAndTimeWindow` → `PerRowWithTimeWindow`; update all three XML doc comment blocks
  - `Adapters/IVendorAdapter.cs`: Update four inline comments to use generic examples
  - `Client/EndpointLoader.cs`: Update error message string (remove "carrier")
  - `Hosting/VendorHostBuilder.cs`: Update one comment (remove "truckerCloudDefaults.json" reference)
  - `TruckerCloudEndpoints.cs`: Update all 8 call sites to use new method names
- **Verification:**
  - `dotnet build ApiLoader.sln` succeeds with zero errors (AC-001)
  - Case-insensitive grep for `CarrierDependent` and `CarrierAndTimeWindow` in `src/` returns zero hits (AC-002, AC-003)
  - Case-insensitive grep for `Carrier` as a code identifier in `src/Canal.Ingestion.ApiLoader/` returns zero hits (AC-002) — note: grep in the core project only; vendor adapters may retain vendor terms in their own code
  - `IVendorAdapter.cs` comments no longer present TruckerCloud as assumed default (AC-004)
  - `EndpointLoader.cs` error message contains no "carrier" reference (AC-005)
- **Invariants preserved:**
  - INV-001: `BuildRequestsDelegate` signature unchanged ✓
  - INV-002: `EndpointDefinition` record unchanged ✓
  - INV-003: `IVendorAdapter` method signatures unchanged ✓
  - INV-005: Parameter types and return types on renamed methods unchanged ✓
  - INV-006: Default parameter values preserved ✓

### Commit 2 — Create design.md (this document)
- **Changes:**
  - Create `design.md` at repo root documenting the completed refactor
- **Verification:**
  - File exists at repo root
- **Invariants preserved:** No code changes; documentation only

## 14. Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Missed call site causes build failure | Low | Very Low | Compiler catches all missing method references. Solution has only 2 adapter projects with clearly known call patterns. |
| Vendor-specific term remains in core project identifier | Low | Low | Post-commit grep audit (AC-002) catches any remaining terms. Complete audit inventory provided in requirements.md. |
| Rename inadvertently changes method signature | Medium | Very Low | Constraint: only rename, no signature changes. Compiler enforces type safety — any signature mismatch causes build error. |
| No automated tests to verify behavioral equivalence | Low | N/A | This is a name-only refactor. No logic, parameters, or return types change. Successful compilation (AC-001) provides strong confidence. |
| Future developers re-introduce vendor terms in core | Low | Medium | DEC-001 documents the policy in `decisions.md`. Consider future CI grep-based lint check (out of scope). |
