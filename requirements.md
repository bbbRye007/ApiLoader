# Requirements: Host Layer Restructuring — Auto-Derived, Per-Vendor CLI Architecture

## 1. Problem Statement
The current monolithic `Canal.Ingestion.ApiLoader.Host` requires extensive manual CLI wiring (parameter parsing, help text, command registration) for every vendor endpoint. With 27 endpoints today and 15-20 new vendor adapters expected in the first two years, this approach does not scale. Developers adding a new vendor must focus on implementing `IVendorAdapter` and `EndpointDefinition`s — not on building a console application. Additionally, the monolithic host prevents independent deployment per vendor, which is required for containerized Kestra orchestration and limits blast radius during debugging and maintenance.

## 2. Stakeholders & Beneficiaries

| Stakeholder | Benefit |
|---|---|
| Developers adding new vendors | Dramatically reduced cognitive load — focus on vendor-specific adapter logic, not CLI plumbing |
| Operations / DevOps | Independent deployment per vendor as Docker containers; reduced blast radius for maintenance and debugging |
| Kestra orchestration flows | Each vendor is a self-contained, independently invocable artifact that can be scheduled and triggered per-endpoint |
| Enterprise architecture | Modular, composable ingestion services aligned with overall architecture goals |

## 3. Functional Requirements

### 3.1 Per-Vendor Independent Deployability
- **FR-001**: Each vendor adapter MUST produce its own independently deployable artifact (console application or equivalent) that can run as a Docker container.
- **FR-002**: The existing monolithic `Canal.Ingestion.ApiLoader.Host` project MUST be deleted upon completion.
- **FR-003**: Both existing vendors (TruckerCloud — 9 endpoints, FMCSA — 19 endpoints) MUST be fully migrated to the new pattern with no loss of functionality.

### 3.2 Auto-Derived CLI Surface
- **FR-004**: The CLI commands, parameters, help text, and validation for each endpoint MUST be derived from existing structured metadata (`EndpointDefinition`, `IVendorAdapter`, `LoadParameters`) rather than hand-coded per endpoint.
- **FR-005**: The `load` command MUST allow invoking a single endpoint by name with endpoint-appropriate parameters. Parameters include but are not limited to:
  - `--max-pages` (optional, all endpoints)
  - `--page-size` (optional, paginated endpoints)
  - `--start-utc` / `--end-utc` (conditional, endpoints with time window support)
  - `--save-behavior` (optional, all endpoints)
  - `--save-watermark` (optional, endpoints supporting watermarks)
  - `--body-params-json` (optional, POST endpoints)
- **FR-006**: Parameters MUST be conditionally presented/required based on what the endpoint actually supports (e.g., time window parameters should not appear for endpoints without time window support; iteration list requirement should be enforced for endpoints that declare `RequiresIterationList`).
- **FR-007**: The CLI MUST generate accurate help text automatically from endpoint metadata, including which parameters are required vs. optional for each endpoint.

### 3.3 Minimal Developer Ceremony for New Vendors
- **FR-008**: A developer adding a new vendor MUST only need to: (a) implement `IVendorAdapter` (or extend `VendorAdapterBase`), (b) define `EndpointDefinition` instances, and (c) perform minimal, well-documented wiring to produce a working CLI — no hand-coding of parameter parsing, help text, or command registration.
- **FR-009**: The shared CLI infrastructure MUST NOT require modification when a new vendor is added, regardless of that vendor's specific configuration needs (authentication method, credential shape, custom HTTP configuration, etc.).

### 3.4 Composition & Configuration
- **FR-010**: The shared infrastructure MUST handle the composition root responsibilities currently in `Program.cs` (configuration loading, logger factory, cancellation token, Azure credentials, HttpClient, `EndpointLoaderFactory`) in a reusable, vendor-agnostic way.
- **FR-011**: Vendor-specific configuration (credentials, base URLs, custom settings) MUST be sourced from `appsettings.json` / `IConfiguration` following existing patterns.
- **FR-012**: The composition mechanism MUST be extensible to accommodate arbitrary vendor constructor requirements (OAuth2, API keys, client certificates, username/password, no auth, or novel mechanisms not yet encountered) without modifying shared infrastructure.

## 4. Non-Functional Requirements
- **NFR-001**: Developer Experience — A mid-level .NET developer should be able to add a new vendor adapter end-to-end without needing to understand the internals of the CLI infrastructure. The mechanism may be sophisticated internally but must be simple to consume.
- **NFR-002**: Vendor Isolation — Adding, modifying, or removing a vendor MUST NOT require changes to any other vendor's code or project.
- **NFR-003**: Backwards Compatibility — Existing storage path conventions, watermark formats, metadata JSON structure, and blob naming must remain unchanged. This is a structural refactoring, not a behavioral change.
- **NFR-004**: Existing Behavior Preservation — All current `EndpointLoader.Load()` behaviors (retry logic, pagination, watermarking, time window resolution, save behaviors) must continue to work identically.
- **NFR-005**: Framework Compatibility — Solution must target .NET 10.0 with nullable reference types and implicit usings, consistent with the existing codebase.
- **NFR-006**: Package Quality — New NuGet package dependencies are acceptable but should be well-maintained, widely adopted libraries.

## 5. Architectural Impact

| Layer | Impact | Notes |
|---|---|---|
| IVendorAdapter | Possibly Modified | May need additions to surface vendor-specific configuration requirements or endpoint metadata for CLI derivation. Constructor variance (credentials shape) is the key challenge. |
| IIngestionStore | None | Storage abstraction unchanged. |
| EndpointDefinition | Possibly Modified | May need additional metadata properties to support CLI parameter derivation (e.g., parameter descriptions, required/optional flags). Currently captures: time window constraints, iteration list requirement, watermark support, page size, HTTP method. Does NOT currently capture: full parameter enumeration, parameter descriptions, required vs. optional flags. |
| FetchEngine | None | HTTP execution unchanged. |
| Model (Request/FetchResult/etc.) | None | Core model chain unchanged. |
| Host (Program.cs) | Deleted | Monolithic host replaced entirely. |
| RequestBuilders | None | Existing builder patterns unchanged. |
| New Project(s) | Yes | Shared CLI infrastructure (library or template), plus per-vendor deployable artifacts. Exact project structure is an architectural decision. |

## 6. Constraints & Boundaries

- **In scope**:
  - Design and implement the new host/CLI architecture
  - Implement the `load` command with full parameter support derived from endpoint metadata
  - Migrate TruckerCloud (9 endpoints) and FMCSA (19 endpoints) to the new pattern
  - Delete the monolithic `Canal.Ingestion.ApiLoader.Host` project
  - Ensure each vendor produces an independently deployable artifact

- **Out of scope**:
  - Structured logging / AppInsights integration (future work)
  - Specific exit codes for Kestra (future work)
  - JSON output mode for machine parsing (future work)
  - Operational commands: list endpoints, check/reset watermark, dry run (Phase 2)
  - Iteration list I/O between CLI invocations — file path output, stdin JSON input (Phase 3)
  - CI/CD pipeline changes, Docker image creation, Kestra flow updates
  - Adding any new vendor adapters (but the pattern must support it)

- **Technology constraints**:
  - .NET 10.0, nullable reference types, implicit usings
  - New NuGet packages acceptable (well-maintained, widely adopted)
  - Must follow existing codebase conventions (storage paths, metadata format, adapter interface)

- **External dependencies**: None — purely local codebase restructuring, validated locally.

## 7. Success Criteria
- **AC-001**: TruckerCloud (all 9 endpoints) can be invoked individually from the command line with endpoint-appropriate parameters, and data lands correctly in storage (ADLS or local filesystem).
- **AC-002**: FMCSA (all 19 endpoints) can be invoked individually from the command line with endpoint-appropriate parameters, and data lands correctly in storage.
- **AC-003**: The monolithic `Canal.Ingestion.ApiLoader.Host` project is deleted with no loss of functionality.
- **AC-004**: Each vendor produces an independently deployable console artifact.
- **AC-005**: CLI help text is auto-generated and accurately reflects each endpoint's supported parameters (required vs. optional, conditional on endpoint capabilities).
- **AC-006**: A developer adding vendor #3 focuses almost entirely on `IVendorAdapter` + `EndpointDefinition`s. The host/CLI layer requires minimal, well-documented steps — no hand-coded parameter parsing, help text, or command registration.
- **AC-007**: The shared CLI infrastructure does not require modification to accommodate vendor #3's configuration needs, even if those needs differ from TruckerCloud and FMCSA.
- **AC-008** (Litmus Test): A developer adding a new vendor should enjoy the experience of focusing on vendor specifics. If they get dragged into CLI plumbing (parameter parsing, help text, command UX), that is a failure.

## 8. Open Questions & Risks

- **OQ-001**: What is the best mechanism for auto-deriving CLI from endpoint metadata — source generators, reflection, a CLI framework like `System.CommandLine`, convention-based discovery, or some combination? — _Status: Open (architect decision)_
- **OQ-002**: Should the per-vendor artifact be a separate `.csproj` console app, or can the vendor class library itself be made executable (change output type)? — _Status: Open (architect decision)_
- **OQ-003**: How should vendor-specific constructor dependencies (varying credential shapes, custom HTTP configuration) be registered in a generic way without the shared infrastructure needing to know about them? — _Status: Open (architect decision — this is the hardest design problem)_
- **OQ-004**: `EndpointDefinition` currently does not capture full parameter metadata (names, descriptions, required vs. optional). How much metadata needs to be added to support CLI derivation, and does this affect existing vendor adapter code? — _Status: Open (architect decision)_
- **OQ-005**: The `BuildRequestsDelegate` is currently an opaque delegate. For CLI derivation, the infrastructure may need to introspect what parameters an endpoint expects. How is this reconciled? — _Status: Open (architect decision)_
- **OQ-006**: One host per vendor vs. one generic host that discovers vendor adapters at runtime — both satisfy the requirements. The architect should evaluate trade-offs (simplicity vs. flexibility, build-time vs. runtime discovery). — _Status: Open (architect decision)_
- **RISK-001**: Attempting to make the CLI too "magic" (fully auto-derived) may hit edge cases where endpoint parameter needs don't fit the metadata model, requiring escape hatches. — _Mitigation: Ensure the architecture supports custom overrides/extensions for endpoints with unusual parameter requirements._
- **RISK-002**: Adding metadata to `EndpointDefinition` to support CLI derivation may create a maintenance burden if the metadata drifts from actual endpoint behavior. — _Mitigation: The metadata should be the single source of truth that drives both CLI and request building, not a parallel declaration._

## 9. Phasing

| Phase | Scope | Dependencies |
|---|---|---|
| **Phase 1 (MVP)** | Core architecture + `load` command with auto-derived parameters. Migrate TruckerCloud and FMCSA. Delete old host. Prove developer experience with both vendors. | None |
| **Phase 2** | Operational commands: list available endpoints, check watermark status, reset watermark, dry run mode. | Phase 1 |
| **Phase 3** | Iteration list I/O: output results as file path / JSON to stdout; accept iteration list as input from prior invocation for efficient Kestra chaining. | Phase 1 |
