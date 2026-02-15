# CLAUDE.md (Repo Operating Manual for AI Assistants)

This file is the **repo-specific source of truth** for how to build, test, run, and safely change this codebase.
It should stay **thin, authoritative, and self-healing**:
- Prefer **discovery commands** over hardcoded inventories.
- Include only what is stable and necessary to prevent mistakes.

Note: `@file.md` means “read `file.md` from repo root” (the actual filename has no `@`).

---

## 0) TL;DR (copy/paste commands)

**Verification stamp (required):**
- Verified by: <name or role>
- Date: <YYYY-MM-DD>
- Environment: <OS + toolchain version, e.g., Windows 11 + .NET 8.0.2>

### Build (VERIFIED)
<PUT EXACT COMMAND(S) HERE>

### Test (VERIFIED or N/A)
<PUT EXACT COMMAND(S) HERE OR "N/A">

### Run / Smoke (VERIFIED or N/A)
<PUT EXACT COMMAND(S) HERE OR "N/A">

### Clean / Reset (optional; VERIFIED or N/A)
<PUT SAFE COMMAND(S) HERE OR "N/A">

Notes (optional):
- <only short, stable notes that reduce mistakes>

---

## Minimum Viable `CLAUDE.md` (MVC)

A repo’s `CLAUDE.md` is considered “minimally usable” only when it contains **verified** entries for the items below.
“Verified” means: derived from repo evidence and/or confirmed by actually running the command successfully.

Required (must exist):
- **Build:** one command that builds the repo (or the primary solution) successfully
- **Test:** one command that runs tests successfully, OR explicitly: “No automated tests / not applicable”
- **Run/Smoke:** one command/procedure that exercises the primary entry point (or a representative executable) and demonstrates it starts/executes without immediate failure.
  - If a meaningful smoke run is not possible, explain why and how to approximate it safely.
- **Discovery commands:** short commands to locate:
  - solution/project list
  - entry points (executables / hosts)
  - test projects
- **Secrets & safety:** what must never be committed; where secrets/config come from (env vars, user-secrets, key vault, etc.)
- **Generated artifacts:** what folders/files are generated and should be ignored

Rules:
- Do **not** guess commands. If unknown, write `TODO/UNKNOWN` plus “How to verify.”
- If a command is unverified, label it **UNVERIFIED** and do not place it in the TL;DR section.
- Prefer the simplest working commands over idealized ones.

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
- Change-scoped docs under \changes/<change>/...` may exist, but they are non-canonical; use them only when the Active* pointers reference them.`

---

## Active Change (Required)

This repo may contain multiple change documents. To prevent ambiguity, the active documents are defined here.

- ActiveDesign: @design.md
- ActiveRequirements: @requirements.md
- ActiveDecisions: @decisions.md

Hard rules:
- These pointers are **authoritative**. Personas must treat them as the only “active” docs for the current work item.
- If an “Active*” pointer references a non-existent file, stop and raise a **[BLOCKER]/[QUESTION]** (do not guess a substitute).
- Every persona output that creates/updates docs must explicitly state which Active* paths it used (one short line).

---

## Decision Log

Durable decisions that affect behavior, scope, invariants, or tradeoffs must be recorded in:
- @decisions.md

Rule of thumb: if a reviewer could reasonably re-argue it later, it must be captured as a DEC entry.

---

## 1) Repo Basics

### Tech stack
- Language/runtime: <e.g., .NET 8 / Node 20 / Python 3.11>
- Package manager: <NuGet / npm / pip / etc.>
- Primary build tool: <dotnet / npm / make / etc.>

### Repo layout (only what matters)
- Source roots: <e.g., src/, services/, apps/>
- Tests: <e.g., tests/, test/>
- Docs: <keep minimal; most docs are in repo root by policy>
- Scripts: <e.g., build/, tools/>

---

## 2) Safety Constraints (non-negotiables)

### Must not do
- <e.g., do not commit secrets; do not reformat unrelated files; do not add new deps without approval>

### Must preserve (high-level invariants)
- <e.g., external API contracts; persisted schema/file formats; CLI/config key stability; etc.>

### Allowed / preferred patterns
- <e.g., logging conventions, DI patterns, async guidelines, naming conventions>

---

## 3) Persona File Contract (team workflow)

This repo uses these canonical files (repo root):

- `requirements.md` — produced by Analyst
- `design.md` — produced by Architect
- `decisions.md` — durable decisions/waivers/policies (Active vs Superseded)
- `ReviewerComments.md` — produced by Reviewer (tagged findings + reviewed range)

Rules:
- Developer implements in small commits based on `design.md`.
- Reviewer reviews committed changes and writes findings only to `ReviewerComments.md`.
- Decisions that change policy or waive risks must be captured in `decisions.md`.
- Do not create duplicates of these files in other folders; move/rename instead.
- If using change-scoped documents, update the **Active Change** pointers rather than inventing new conventions.

---

## 4) Configuration & Precedence (if applicable)

### Configuration sources (highest wins)
1) <CLI args?>
2) <Environment variables?>
3) <Config files?>
4) <Defaults?>

List key config files and where they live:
- <path>
- <path>

Note: document only precedence and required keys; do not copy full config files into this doc.

---

## 5) Discovery Commands (self-healing guidance)

Use these commands to discover current repo state instead of hardcoding it here.

### Project / module inventory
- <e.g., `dotnet sln <solution.sln> list`>
- <e.g., `find . -name "*.csproj"`>

### Entry points / run targets
- <e.g., `dotnet run --project <path>` (discover candidates via search)>
- <e.g., `rg "static void Main|CreateHostBuilder|Program.cs"`>

### Key conventions discovery
- <e.g., `rg "IServiceCollection|AddSingleton|AddHostedService"` for DI patterns>
- <e.g., `rg "ILogger<|LogInformation"` for logging patterns>

### Tests discovery
- <e.g., `dotnet test` plus listing projects>

---

## 6) Common Workflows

### Add a dependency
- <commands + any repo rules>

### Add a new project/module
- <commands + any repo rules>
- Note: if this changes build/run behavior, update this file in the same commit.

### Local dev / debugging notes
- <only stable, high-value notes>

---

## 7) Danger Zones

- Generated code: <paths and rules>
- Secrets/config: <paths to ignore; examples of what must not be committed>
- Large files / binaries: <rules>
- Platform dependencies: <Windows-only/Linux-only constraints>

---

## 8) Change Log Policy for This File

Update `CLAUDE.md` only when:
- Build/test/run commands change
- Repo safety constraints/invariants change
- Persona file contract changes
- Discovery commands need correction

Treat `CLAUDE.md` as repo data: generated from the template initially, then updated only when repo reality changes.

If `CLAUDE.md` is missing or stale, the first commit in a change series must be:
- `chore: bootstrap/fix CLAUDE.md`
and subsequent commits may rely on it.

Do NOT maintain volatile inventories here (e.g., a list of every project) unless the repo is stable and that list rarely changes.
Prefer discovery commands instead.
