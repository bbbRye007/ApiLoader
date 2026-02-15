# CLAUDE.md (Repo Operating Manual for AI Assistants)

This file is the **repo-specific source of truth** for how to build, test, run, and safely change this codebase.

---

## 0) TL;DR (copy/paste commands)

**Verification stamp (required):**
- Verified by: Developer (build succeeded: 0 warnings, 0 errors)
- Date: 2026-02-15
- Environment: Windows + .NET 10.0

### Build
```
dotnet build ApiLoader.sln
```

### Test (N/A)
No automated test projects exist in this solution.

### Run / Smoke
```
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- list
dotnet run --project src/Canal.Ingestion.ApiLoader.Host.Fmcsa -- list
```
Note: `list` subcommand prints endpoint catalog — requires no credentials or external services.

### Clean / Reset (optional)
```
dotnet clean ApiLoader.sln
```

---

## Canonical Doc Location (MANDATORY)

All persona workflow files and repo-contract docs live in the **repo root**:

- `CLAUDE.md`
- `CLAUDE_TEMPLATE.md`
- `requirements.md`
- `design.md`
- `decisions.md`
- `ReviewerComments.md`

Rules:
- The repo-root files listed above are the only canonical copies; do not duplicate them elsewhere.

---

## Active Change (Required)

- ActiveDesign: @design.md
- ActiveRequirements: @requirements.md
- ActiveDecisions: @decisions.md

Hard rules:
- These pointers are **authoritative**. Personas must treat them as the only "active" docs for the current work item.
- If an "Active*" pointer references a non-existent file, stop and raise a **[BLOCKER]/[QUESTION]** (do not guess a substitute).

---

## Decision Log

Durable decisions that affect behavior, scope, invariants, or tradeoffs must be recorded in:
- @decisions.md

---

## 1) Repo Basics

### Tech stack
- Language/runtime: C# / .NET 10.0
- Package manager: NuGet
- Primary build tool: dotnet CLI

### Repo layout (only what matters)
- Source roots: `src/`
- Tests: None (no test projects exist)
- Scripts: `exerciseEndpoints.bat`, `publishHost.bat` (Windows batch files)

### Solution projects (7 projects)
| Project | Role |
|---|---|
| `Canal.Ingestion.ApiLoader` | Core engine library (vendor-agnostic) |
| `Canal.Ingestion.ApiLoader.Hosting` | Shared CLI hosting framework |
| `Canal.Ingestion.ApiLoader.TruckerCloud` | TruckerCloud vendor adapter library |
| `Canal.Ingestion.ApiLoader.Fmcsa` | FMCSA vendor adapter library |
| `Canal.Ingestion.ApiLoader.Host.TruckerCloud` | TruckerCloud host executable |
| `Canal.Ingestion.ApiLoader.Host.Fmcsa` | FMCSA host executable |
| `Canal.Storage.Adls` | Azure Data Lake Storage client library |

---

## 2) Safety Constraints (non-negotiables)

### Must not do
- Do not commit secrets (API keys, connection strings, client secrets)
- Do not reformat unrelated files in a refactor commit
- Do not add new NuGet dependencies without approval

### Must preserve (high-level invariants)
- `BuildRequestsDelegate` delegate signature must not change
- `EndpointDefinition` record structure must not change
- `IVendorAdapter` interface contract (method signatures) must not change
- Method signatures and return types on `RequestBuilders` — only names may change during renames
- Default parameter values that represent industry-standard formats (e.g., ISO-8601 time format)

### Allowed / preferred patterns
- Vendor-agnostic naming in the core engine project
- Vendor-specific names belong only in vendor adapter projects
- XML doc comments in core may reference vendors only as proof of engine generality (FR-004 policy)

---

## 3) Persona File Contract (team workflow)

- `requirements.md` — produced by Analyst
- `design.md` — produced by Architect
- `decisions.md` — durable decisions/waivers/policies
- `ReviewerComments.md` — produced by Reviewer

Rules:
- Developer implements in small commits based on `design.md`.
- Reviewer reviews committed changes and writes findings only to `ReviewerComments.md`.
- Decisions that change policy or waive risks must be captured in `decisions.md`.

---

## 4) Configuration & Precedence

### Configuration sources (highest wins)
1) CLI args (System.CommandLine options)
2) Environment variables
3) `appsettings.json` (optional, in working directory)
4) Vendor-specific embedded defaults (e.g., `truckerCloudDefaults.json`)
5) Shared hosting defaults (`sharedDefaults.json` embedded resource)

---

## 5) Discovery Commands

### Project / module inventory
```
dotnet sln ApiLoader.sln list
```

### Entry points / run targets
Host executables:
- `src/Canal.Ingestion.ApiLoader.Host.TruckerCloud/`
- `src/Canal.Ingestion.ApiLoader.Host.Fmcsa/`

### Key conventions discovery
```
rg "IVendorAdapter|VendorAdapterBase" src/ --type cs
rg "RequestBuilders\." src/ --type cs
rg "EndpointDefinition" src/ --type cs
```

---

## 6) Common Workflows

### Add a new vendor adapter
1. Create new class library project: `Canal.Ingestion.ApiLoader.<VendorName>`
2. Implement `VendorAdapterBase` subclass
3. Create `<VendorName>Endpoints.cs` static class with `EndpointDefinition` fields
4. Create host executable project: `Canal.Ingestion.ApiLoader.Host.<VendorName>`

---

## 7) Danger Zones

- Generated code: `obj/`, `bin/` directories (in `.gitignore`)
- Secrets/config: API keys, Azure credentials — must come from env vars or appsettings.json (not committed)
- Platform dependencies: `publishHost.bat` and `exerciseEndpoints.bat` are Windows-only
