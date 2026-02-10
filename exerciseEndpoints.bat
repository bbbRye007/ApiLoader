@echo off
setlocal
echo ============================================================
echo   ApiLoader CLI Exercise Script
echo   Uses --storage file (local) so no Azure credentials needed
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
echo === 1. HELP ===
echo.
"%EXE%" --help
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 2. LIST — all vendors ===
echo.
"%EXE%" list
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 3. LIST — TruckerCloud verbose ===
echo.
"%EXE%" list --vendor truckercloud --verbose
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 4. LIST — FMCSA verbose ===
echo.
"%EXE%" list --vendor fmcsa --verbose
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 5. DRY RUN — load truckercloud DriversV4 (shows dependency chain) ===
echo.
"%EXE%" load truckercloud DriversV4 %COMMON% --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 6. DRY RUN — load truckercloud TripsV5 (shows dependency chain + dates) ===
echo.
"%EXE%" load truckercloud TripsV5 %COMMON% --start "01/15/2026" --end "01/15/2026 23:59:59" --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 7. DRY RUN — load truckercloud SafetyEventsV5 with date range ===
echo.
"%EXE%" load truckercloud SafetyEventsV5 %COMMON% --start 2026-01-01 --end 2026-01-07 --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 8. DRY RUN — load fmcsa CompanyCensus (no dependencies) ===
echo.
"%EXE%" load fmcsa CompanyCensus %COMMON% --max-pages 2 --dry-run
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 9. LOAD — truckercloud CarriersV4 (1 page, local file storage) ===
echo.
"%EXE%" load truckercloud CarriersV4 %COMMON% --max-pages 1
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 10. LOAD — fmcsa CompanyCensus (1 page, local file storage) ===
echo.
"%EXE%" load fmcsa CompanyCensus %COMMON% --max-pages 1
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === 11. TEST — fmcsa only, 1 page each (local file storage) ===
echo.
"%EXE%" test --vendor fmcsa %COMMON% --max-pages 1
echo.
echo ────────────────────────────────────────────────────────────
pause

echo.
echo === DONE ===
echo.
echo Output was saved to: %~dp0ingestion-output
echo.
pause
