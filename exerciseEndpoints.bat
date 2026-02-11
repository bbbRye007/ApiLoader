@echo off
setlocal
echo ============================================================
echo   ApiLoader CLI Exercise Script
echo   Uses --storage file (local) so no Azure credentials needed
echo   Output: C:\Temp\ApiLoaderOutput
echo ============================================================
echo.

set EXE=%~dp0publish\Canal.Ingestion.ApiLoader.Host.exe

if not exist "%EXE%" (
    echo ERROR: %EXE% not found.
    echo Run publishHost.bat first.
    pause
    exit /b 1
)

set COMMON=--storage file --environment EXERCISE

echo.
echo ************************************************************
echo   PHASE 1: DISCOVERY (no API calls, no data fetched)
echo ************************************************************
echo.
pause

echo.
echo --- Help ---
echo.
"%EXE%" --help
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- List all vendors ---
echo.
"%EXE%" list
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- List TruckerCloud endpoints (verbose) ---
echo.
"%EXE%" list --vendor truckercloud --verbose
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- List FMCSA endpoints (verbose) ---
echo.
"%EXE%" list --vendor fmcsa --verbose
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo ************************************************************
echo   PHASE 2: DRY RUNS (no API calls — previews what WOULD happen)
echo ************************************************************
echo.
pause

echo.
echo --- DriversV4: shows CarriersV4 dependency chain ---
echo.
"%EXE%" load truckercloud DriversV4 %COMMON% --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- TripsV5: dependency chain + 1-day date window ---
echo.
"%EXE%" load truckercloud TripsV5 %COMMON% --start "01/15/2026" --end "01/15/2026 23:59:59" --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- SafetyEventsV5: dependency chain + week-long date range ---
echo.
"%EXE%" load truckercloud SafetyEventsV5 %COMMON% --start 2026-01-01 --end 2026-01-07 --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- FMCSA CompanyCensus: no dependencies ---
echo.
"%EXE%" load fmcsa CompanyCensus %COMMON% --max-pages 2 --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo ************************************************************
echo   PHASE 3: LIVE LOADS (calls APIs, saves to local files)
echo   Output goes to: C:\Temp\ApiLoaderOutput
echo ************************************************************
echo.
pause

echo.
echo --- Load truckercloud CarriersV4 (1 page) ---
echo.
"%EXE%" load truckercloud CarriersV4 %COMMON% --max-pages 1
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- Load fmcsa CompanyCensus (1 page) ---
echo.
"%EXE%" load fmcsa CompanyCensus %COMMON% --max-pages 1
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- Test suite: fmcsa only, 1 page each ---
echo.
"%EXE%" test --vendor fmcsa %COMMON% --max-pages 1
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo ************************************************************
echo   DONE
echo   Output was saved to: C:\Temp\ApiLoaderOutput
echo ************************************************************
echo.
pause
