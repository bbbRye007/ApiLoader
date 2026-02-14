# Code Review: d6d2f13 — Fix per-invocation disposal and cancellation token wiring

**Scope:** `git diff HEAD~1..HEAD` — 3 files, +29/−9 lines
**Warning:** Uncommitted files detected (`comments.md`, `x`).

## Verdict on prior-round fixes

| Prior finding | Status | Notes |
|---|---|---|
| S1 blocker: resource leaks (HttpClient, LoggerFactory, CTS) | **Fixed** | `LoadContext : IDisposable` owns all three; `using var ctx` at call site |
| S2 warning: dual cancellation, S.CL token ignored | **Fixed** | `CreateLinkedTokenSource(commandToken, processCts.Token)` composes both sources |
| No unrelated refactors | **Clean** | All 3 files are scoped to disposal + cancellation; no drive-by changes |

---

```mermaid
mindmap
  root((Review d6d2f13))
    LoadCommandBuilder.cs
      L135 blocker: `using var ctx` is OUTSIDE the try/catch — previously contextFactory was inside the try block, so exceptions from infrastructure setup e.g. bad Azure credentials, missing config were caught and returned exit code 1 with a message; now they propagate unhandled and crash with a raw stack trace
        Fix: move `using var ctx` inside the try block — `using var` scoped inside try still disposes correctly before catch runs
    VendorHostBuilder.cs
      L201 warning: `processCts` is created but never disposed — only `linkedCts` is tracked in LoadContext; disposing `linkedCts` does NOT dispose its source tokens, so `processCts` leaks
        Fix: add `processCts` to LoadContext disposables, or nest it — `processCts.Dispose()` after `linkedCts.Dispose()` in LoadContext.Dispose
      L207-209 warning: event handlers registered on AssemblyLoadContext.Unloading, ProcessExit, CancelKeyPress capture `processCts` in their closures and are never unregistered — each call to BuildLoadContext adds a new set of handlers that accumulate; benign for single-command CLI today but a leak if the factory is ever invoked more than once per process
      L199-212 nit: comment says "either source triggers cancellation" which is correct, but does not mention that processCts itself is untracked for disposal — future reader may assume everything is cleaned up
    LoadContext.cs
      L24-28 nit: if LinkedCts.Dispose throws, HttpClient and LoggerFactory leak — Dispose implementations should not throw in practice, but defensive code would wrap each in try/finally or use a helper
      L20-22 nit: LoggerFactory, HttpClient, LinkedCts are public required init properties — exposes disposal-owned internals to all internal callers; consider private set or a dedicated disposables struct to prevent external misuse
```

---

## Priority Summary

| Severity | Count | Key items |
|----------|-------|-----------|
| Blocker  | 1     | `using var ctx` outside try — infrastructure setup exceptions now unhandled (regression from the move) |
| Warning  | 2     | `processCts` not disposed; event handlers accumulate per-invocation |
| Nit      | 3     | Missing disposal comment, no defensive try/finally in Dispose, public disposable properties |
