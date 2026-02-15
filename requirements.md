# Requirements: Vendor-Agnostic Core Engine Refactor

Active docs used: ActiveRequirements: @requirements.md, ActiveDesign: @design.md (does not yet exist), ActiveDecisions: @decisions.md (does not yet exist)

## 1. Problem Statement

The core engine project (`Canal.Ingestion.ApiLoader`) contains vendor-specific "Carrier" semantics in its `Engine.RequestBuilders` class — method names, comments, and default parameter values that originate from TruckerCloud's domain model. Additionally, `IVendorAdapter.cs` XML docs cite TruckerCloud-specific examples as if they are the assumed default, and `EndpointLoader.cs` contains a vendor-specific term in an error message. A new vendor adapter is imminent, and the team wants clean architectural boundaries established so that new adapter authors encounter only generic engine primitives — never vendor-specific naming or assumptions.

## 2. Context & Classification

- **Project maturity:** Established (two vendor adapters: TruckerCloud, FMCSA)
- **Change type:** Refactor (rename + comment cleanup; no new features or behavioral changes)
- **Surface type:** Shared library (core engine consumed by vendor adapter libraries and host executables)
- **Breaking change tolerance:** Allowed (pre-production; rename methods and update all call sites in same change)
- **Consumers:** Vendor adapter libraries (`Canal.Ingestion.ApiLoader.TruckerCloud`, `Canal.Ingestion.ApiLoader.Fmcsa`), hosting framework (`Canal.Ingestion.ApiLoader.Hosting`), host executables
- **Operational context:** Pre-production; not yet deployed to production

## 3. Stakeholders & Beneficiaries

- **Future vendor adapter developers** — primary beneficiary; will encounter only generic engine concepts when implementing new integrations
- **Current maintainers** — cleaner layering reduces cognitive load
- **The incoming vendor integration** — clean foundation to build on without confusion about whether "Carrier" patterns are generic or TruckerCloud-locked

## 4. Scope

### In Scope
- Rename vendor-specific method names in `Canal.Ingestion.ApiLoader.Engine.RequestBuilders`
- Update all XML doc comments and inline comments in the core project (`Canal.Ingestion.ApiLoader`) to remove vendor-specific language from code identifiers; comments may reference specific vendors as *proof of the engine's generality* (see FR-004)
- Update the error message string in `Canal.Ingestion.ApiLoader.Client.EndpointLoader.cs` to use vendor-agnostic language
- Update IVendorAdapter.cs XML doc examples to use generic placeholders or reframe vendor mentions as proof of flexibility
- Update all vendor adapter call sites (`TruckerCloudEndpoints.cs`, `FmcsaEndpoints.cs`) to use new method names — clean break, no stale names
- Verify the solution compiles successfully after all renames

### Out of Scope
- No new features or capabilities
- No changes to `FetchEngine.cs` logic, retry behavior, or pagination mechanics
- No changes to `IVendorAdapter` or `VendorAdapterBase` interface contracts (beyond comment cleanup)
- No changes to `Model/` types (`Request`, `FetchResult`, `EndpointDefinition`, `LoadParameters`, etc.) unless vendor-specific terms are found in their identifiers
- No changes to `Storage/` layer
- No structural changes to the adapter pattern itself
- No changes to `Canal.Ingestion.ApiLoader.Hosting` project (unless vendor terms leak into hosting framework code)
- No test creation (no test projects currently exist in the solution)

## 5. Functional Requirements

- **FR-001**: All public method names in `Canal.Ingestion.ApiLoader.Engine.RequestBuilders` must use vendor-agnostic terminology. Specifically, `CarrierDependent` and `CarrierAndTimeWindow` must be renamed to generic terms that describe the *pattern* (e.g., iteration-over-rows, iteration-with-time-window) rather than referencing any vendor's domain entity. Final naming deferred to Architect.

- **FR-002**: All code identifiers (method names, parameter names, variable names, type names) across the entire `Canal.Ingestion.ApiLoader` core project must be free of vendor-specific terms. "Vendor-specific terms" includes but is not limited to: `Carrier` (when used as a domain entity name), `TruckerCloud`, `FMCSA`, `Telematics`, `ELD`, `EldVendor`, and specific TruckerCloud endpoint names (`Drivers`, `RiskScores`, `SafetyEvents`, `GpsMiles`, `RadiusOfOperation`, `ZipCodeMiles`).

- **FR-003**: The `EndpointLoader.cs` error message that currently reads `"e.g., carrier results from a prior Load() call"` must be replaced with vendor-agnostic guidance.

- **FR-004**: XML doc comments and inline comments in the core project may reference specific vendors (TruckerCloud, FMCSA) **only** when framed as proof of the engine's generality — e.g., "Even non-standard vendors like TruckerCloud are supported via this pattern." Comments must not present any vendor as the assumed default or primary use case.

- **FR-005**: All vendor adapter call sites that reference the renamed methods must be updated in the same change:
  - `Canal.Ingestion.ApiLoader.TruckerCloud/TruckerCloudEndpoints.cs`
  - `Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs`
  - Any other files in the solution that reference the old method names

- **FR-006**: Sensible defaults for method parameters (e.g., ISO-8601 time format `"yyyy-MM-dd'T'HH:mm:ss'Z'"`) should be preserved to minimize cognitive load for new adapter authors. Vendors override only what differs from the defaults. No defaults should be removed solely because they originated from TruckerCloud conventions, provided they represent reasonable industry-standard values.

## 6. Data / Contracts / Compatibility

### 6.1 External Contracts (hard invariants)
- N/A — this is an internal refactor with no external API surface changes.

### 6.2 Persisted Artifacts (durable shapes)
- N/A — no persisted data formats are affected by method renames.

### 6.3 Compatibility Requirements (soft invariants)
- The `BuildRequestsDelegate` delegate signature must not change. The refactor is limited to the names of factory methods that *produce* delegates, not the delegate type itself.
- The `EndpointDefinition` record structure must not change.
- The `IVendorAdapter` interface contract must not change (only doc comments are updated).

## 7. Non-Functional Requirements

- **NFR-001**: [Developer Experience] — A developer implementing a new `VendorAdapter` must be able to consume the core engine without encountering any vendor-specific naming. Their cognitive load should be focused entirely on their vendor's specifics, not on understanding another vendor's domain model.

- **NFR-002**: [Maintainability] — The renamed method names must be self-documenting: a developer reading `RequestBuilders.XYZ(...)` should understand the *pattern* (iteration, time-window, etc.) without needing to know any vendor's domain model.

- **NFR-003**: [Build Integrity] — The solution must compile cleanly (`dotnet build ApiLoader.sln`) after all changes with zero errors and zero new warnings introduced by the refactor.

## 8. Constraints & Boundaries

- **Technology constraints:** .NET 10.0; no new dependencies may be introduced.
- **External dependencies:** None — pure internal refactor.
- **Prohibited approaches:**
  - Do not introduce `[Obsolete]` wrappers or backward-compatible shims; this is a clean break.
  - Do not change method signatures, parameter types, or return types — only names and comments.
  - Do not restructure the `RequestBuilders` class (e.g., splitting into multiple classes, introducing inheritance) — that is a separate concern.
- **Known limitations:** No automated test suite exists to validate behavioral equivalence. Verification relies on successful compilation and grep-based audit.

## 9. Acceptance Criteria

- **AC-001**: `dotnet build ApiLoader.sln` succeeds with zero errors.

- **AC-002**: A case-insensitive grep for the following terms in **code identifiers** (method names, parameter names, variable names, type names, string literals) within the `Canal.Ingestion.ApiLoader` core project returns zero hits:
  - `CarrierDependent` (as a method name)
  - `CarrierAndTimeWindow` (as a method name)
  - Any code identifier containing `Carrier` as a domain entity (note: generic English usage in comments framed as proof-of-generality is allowed per FR-004)

- **AC-003**: All vendor adapter call sites (`TruckerCloudEndpoints.cs`, `FmcsaEndpoints.cs`) reference the new method names and compile successfully.

- **AC-004**: XML doc comments on `IVendorAdapter` properties no longer present TruckerCloud as the assumed/default vendor. Any vendor mentions in comments are framed as demonstrating engine flexibility.

- **AC-005**: The `EndpointLoader.cs` error message uses vendor-agnostic language (no reference to "carrier").

## 10. Failure Scenarios (must-handle)

- **FS-001**: A renamed method is missed in a vendor adapter call site → Build failure. **Expected:** Caught by AC-001 (compiler error on missing method).
- **FS-002**: A vendor-specific term remains in a core project code identifier after refactor → **Expected:** Caught by AC-002 (grep audit).
- **FS-003**: A method rename inadvertently changes method signature or behavior → **Expected:** Prevented by Constraint "do not change signatures." Verified by AC-001 (type-safe compilation).

## 11. Prioritization / Phasing

| Item | Priority | Notes |
|---|---:|---|
| Rename `CarrierDependent` and `CarrierAndTimeWindow` methods | Must | Primary contamination; method names are the public API surface |
| Update all vendor adapter call sites | Must | Required for compilation; same change as renames |
| Update `EndpointLoader.cs` error message | Must | String literal with vendor term |
| Update `IVendorAdapter.cs` XML doc comments | Must | Interface docs are first thing new adapter authors read |
| Update `RequestBuilders.cs` inline comments | Must | Comments reference vendor-specific endpoint names |
| Review default parameter values for vendor bias | Should | Keep ISO-8601 defaults; document the rationale |
| Full grep audit of core project for any remaining leaks | Must | Final verification step |

## 12. Open Questions & Risks

- **OQ-001**: What should the new method names be for `CarrierDependent` and `CarrierAndTimeWindow`? — _Status: Open. Deferred to Architect. Candidates include: `IterationBased`/`IterationWithTimeWindow`, `RowDependent`/`RowDependentWithTimeWindow`, `DependentEndpoint`/`DependentEndpointWithTimeWindow`._

- **RISK-001**: No automated test suite exists to verify behavioral equivalence after rename. — _Mitigation: This is a name-only refactor with no signature or logic changes. Successful compilation (AC-001) provides strong confidence. Behavioral risk is minimal._

- **RISK-002**: Future developers may re-introduce vendor-specific terms in the core project. — _Mitigation: Document the vendor-agnostic policy in a `decisions.md` entry. Consider a future CI grep-based lint check (out of scope for this change)._

## 13. Architectural Impact (best-effort)

- **Affected surfaces:**
  - `Canal.Ingestion.ApiLoader.Engine.RequestBuilders` — method renames
  - `Canal.Ingestion.ApiLoader.Adapters.IVendorAdapter` — comment updates only
  - `Canal.Ingestion.ApiLoader.Client.EndpointLoader` — error message update
  - `Canal.Ingestion.ApiLoader.TruckerCloud.TruckerCloudEndpoints` — call site updates
  - `Canal.Ingestion.ApiLoader.Fmcsa.FmcsaEndpoints` — call site updates (currently uses only `Simple`, but verify no other references)

- **Likely touched components:**
  - `Engine/RequestBuilders.cs` (primary)
  - `Adapters/IVendorAdapter.cs` (comments only)
  - `Client/EndpointLoader.cs` (error message)
  - Vendor adapter endpoint catalogs (call sites)

- **Likely touched boundaries:**
  - Core engine ↔ vendor adapter boundary (method names are the contract surface)
  - No storage, hosting, or model boundaries affected

---

### Audit Inventory (reference for Architect/Developer)

Complete list of vendor-specific references found in the core project during analysis:

| File | Line(s) | Term | Location Type |
|---|---|---|---|
| `Engine/RequestBuilders.cs` | 20, 34 | `CarrierDependent`, `CarrierAndTimeWindow` | Method names |
| `Engine/RequestBuilders.cs` | 9 | "Carriers, Vehicles, all FMCSA" | Comment |
| `Engine/RequestBuilders.cs` | 18 | "Drivers, RiskScores where each carrier" | Comment |
| `Engine/RequestBuilders.cs` | 32 | "SafetyEvents, GpsMiles, RadiusOfOperation, ZipCodeMiles" | Comment |
| `Adapters/IVendorAdapter.cs` | 8 | `"Telematics"` example | XML doc comment |
| `Adapters/IVendorAdapter.cs` | 9 | `"TruckerCloud"` example | XML doc comment |
| `Adapters/IVendorAdapter.cs` | 10 | `"https://api.truckercloud.com/api/"` example | XML doc comment |
| `Adapters/IVendorAdapter.cs` | 12 | `"Trucker Cloud Apis"` | XML doc comment |
| `Client/EndpointLoader.cs` | 36 | `"carrier results from a prior Load() call"` | String literal |
