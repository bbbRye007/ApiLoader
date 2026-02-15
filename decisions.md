# Decisions Log

Durable decisions that affect behavior, scope, invariants, or tradeoffs.

---

## DEC-001: Vendor-Agnostic Core Engine Policy

- **Date:** 2026-02-15
- **Status:** Active
- **Context:** The core engine project (`Canal.Ingestion.ApiLoader`) must be vendor-agnostic. A new vendor adapter is imminent, and adapter authors should encounter only generic engine primitives.
- **Decision:** All code identifiers (method names, parameter names, variable names, type names) in the core engine project must be free of vendor-specific terms. Comments may reference vendors only when framed as proof of the engine's generality — never as the assumed default.
- **Consequences:** Existing `CarrierDependent` and `CarrierAndTimeWindow` method names must be renamed. All call sites in vendor adapters must be updated in the same change.
- **Supersedes:** N/A

---

## DEC-002: Breaking Changes Allowed (Pre-Production)

- **Date:** 2026-02-15
- **Status:** Active
- **Context:** The system is pre-production. No external consumers, no deployed instances, no backward-compatibility contracts to preserve.
- **Decision:** Breaking changes to internal method names are allowed. No `[Obsolete]` shims, no dual-naming period. Clean break: rename and update all call sites atomically.
- **Consequences:** Simpler, cleaner change. No tech debt from temporary compatibility layers.
- **Supersedes:** N/A

---

## DEC-003: Invariant Policy — Public API and Delegate Signatures Stable

- **Date:** 2026-02-15
- **Status:** Active
- **Context:** While method names may change, the structural contracts between core engine and vendor adapters must remain stable.
- **Decision:**
  - `BuildRequestsDelegate` delegate signature: **hard invariant** (must not change)
  - `EndpointDefinition` record shape: **hard invariant** (must not change)
  - `IVendorAdapter` interface method signatures: **hard invariant** (must not change; comments may change)
  - `RequestBuilders` method signatures (parameter types and return types): **hard invariant** (only names change)
  - Default parameter values representing industry standards (ISO-8601 format): **soft invariant** (preserve unless explicitly overridden)
- **Consequences:** The rename refactor is strictly name-only. Any accidental signature change is caught by the compiler.
- **Supersedes:** N/A
