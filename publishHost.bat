@echo off
echo Publishing Canal.Ingestion.ApiLoader.Host...
dotnet publish "%~dp0src\Canal.Ingestion.ApiLoader.Host\Canal.Ingestion.ApiLoader.Host.csproj" -c Release -r win-x64 -o "%~dp0publish"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo PUBLISH FAILED.
    pause
    exit /b 1
)
echo.
echo Published to: %~dp0publish
echo.
dir "%~dp0publish"
pause
