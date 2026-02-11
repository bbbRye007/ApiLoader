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
set FAIL=0

echo.
echo ************************************************************
echo   PHASE 1: DISCOVERY (no API calls, no data fetched)
echo ************************************************************
echo.
pause

echo.
echo --- Help ---
echo RUNNING: %EXE% --help
echo.
"%EXE%" --help
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- List all vendors ---
echo RUNNING: %EXE% list
echo.
"%EXE%" list
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- List TruckerCloud endpoints (verbose) ---
echo RUNNING: %EXE% list --vendor truckercloud --verbose
echo.
"%EXE%" list --vendor truckercloud --verbose
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- List FMCSA endpoints (verbose) ---
echo RUNNING: %EXE% list --vendor fmcsa --verbose
echo.
"%EXE%" list --vendor fmcsa --verbose
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
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
echo RUNNING: %EXE% load truckercloud DriversV4 %COMMON% --dry-run
echo.
"%EXE%" load truckercloud DriversV4 %COMMON% --dry-run
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- TripsV5: dependency chain + 1-day date window ---
echo RUNNING: %EXE% load truckercloud TripsV5 %COMMON% --start "01/15/2026" --end "01/15/2026 23:59:59" --dry-run
echo.
"%EXE%" load truckercloud TripsV5 %COMMON% --start "01/15/2026" --end "01/15/2026 23:59:59" --dry-run
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- SafetyEventsV5: dependency chain + week-long date range ---
echo RUNNING: %EXE% load truckercloud SafetyEventsV5 %COMMON% --start 2026-01-01 --end 2026-01-07 --dry-run
echo.
"%EXE%" load truckercloud SafetyEventsV5 %COMMON% --start 2026-01-01 --end 2026-01-07 --dry-run
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- FMCSA CompanyCensus: no dependencies ---
echo RUNNING: %EXE% load fmcsa CompanyCensus %COMMON% --max-pages 2 --dry-run
echo.
"%EXE%" load fmcsa CompanyCensus %COMMON% --max-pages 2 --dry-run
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
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
echo RUNNING: %EXE% load truckercloud CarriersV4 %COMMON% --max-pages 1
echo.
"%EXE%" load truckercloud CarriersV4 %COMMON% --max-pages 1
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- Load fmcsa CompanyCensus (1 page) ---
echo RUNNING: %EXE% load fmcsa CompanyCensus %COMMON% --max-pages 1
echo.
"%EXE%" load fmcsa CompanyCensus %COMMON% --max-pages 1
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo --- Test suite: fmcsa only, 1 page each ---
echo RUNNING: %EXE% test --vendor fmcsa %COMMON% --max-pages 1
echo.
"%EXE%" test --vendor fmcsa %COMMON% --max-pages 1
if %ERRORLEVEL% NEQ 0 ( echo. & echo *** FAILED [exit code %ERRORLEVEL%] *** & set FAIL=1 )
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo ************************************************************
if %FAIL% EQU 1 (
    echo   DONE — ONE OR MORE STEPS FAILED (see above)
) else (
    echo   DONE — ALL STEPS PASSED
)
echo   Output was saved to: C:\Temp\ApiLoaderOutput
echo ************************************************************
echo.
pause
