[1mdiff --git a/ApiLoader.sln b/ApiLoader.sln[m
[1mindex bd23340..f5f0dd7 100644[m
[1m--- a/ApiLoader.sln[m
[1m+++ b/ApiLoader.sln[m
[36m@@ -1,4 +1,4 @@[m
[31m-[m
[32m+[m[32m﻿[m
 Microsoft Visual Studio Solution File, Format Version 12.00[m
 # Visual Studio Version 17[m
 VisualStudioVersion = 17.0.31903.59[m
[36m@@ -13,40 +13,117 @@[m [mProject("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Canal.Ingestion.ApiLoader.T[m
 EndProject[m
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Canal.Ingestion.ApiLoader.Fmcsa", "src\Canal.Ingestion.ApiLoader.Fmcsa\Canal.Ingestion.ApiLoader.Fmcsa.csproj", "{A1B2C3D4-4444-4444-4444-444444444444}"[m
 EndProject[m
[31m-Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Canal.Ingestion.ApiLoader.Host", "src\Canal.Ingestion.ApiLoader.Host\Canal.Ingestion.ApiLoader.Host.csproj", "{A1B2C3D4-5555-5555-5555-555555555555}"[m
[32m+[m[32mProject("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Canal.Ingestion.ApiLoader.Hosting", "src\Canal.Ingestion.ApiLoader.Hosting\Canal.Ingestion.ApiLoader.Hosting.csproj", "{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}"[m
[32m+[m[32mEndProject[m
[32m+[m[32mProject("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Canal.Ingestion.ApiLoader.Host.TruckerCloud", "src\Canal.Ingestion.ApiLoader.Host.TruckerCloud\Canal.Ingestion.ApiLoader.Host.TruckerCloud.csproj", "{132767F0-26E5-4C9E-8196-5F39C24B1D10}"[m
[32m+[m[32mEndProject[m
[32m+[m[32mProject("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Canal.Ingestion.ApiLoader.Host.Fmcsa", "src\Canal.Ingestion.ApiLoader.Host.Fmcsa\Canal.Ingestion.ApiLoader.Host.Fmcsa.csproj", "{E04EB041-8095-41EB-A53D-6B5488D1C081}"[m
 EndProject[m
 Global[m
 	GlobalSection(SolutionConfigurationPlatforms) = preSolution[m
 		Debug|Any CPU = Debug|Any CPU[m
[32m+[m		[32mDebug|x64 = Debug|x64[m
[32m+[m		[32mDebug|x86 = Debug|x86[m
 		Release|Any CPU = Release|Any CPU[m
[32m+[m		[32mRelease|x64 = Release|x64[m
[32m+[m		[32mRelease|x86 = Release|x86[m
 	EndGlobalSection[m
 	GlobalSection(ProjectConfigurationPlatforms) = postSolution[m
 		{A1B2C3D4-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
 		{A1B2C3D4-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Debug|x86.Build.0 = Debug|Any CPU[m
 		{A1B2C3D4-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
 		{A1B2C3D4-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-1111-1111-1111-111111111111}.Release|x86.Build.0 = Release|Any CPU[m
 		{A1B2C3D4-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
 		{A1B2C3D4-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Debug|x86.Build.0 = Debug|Any CPU[m
 		{A1B2C3D4-2222-2222-2222-222222222222}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
 		{A1B2C3D4-2222-2222-2222-222222222222}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-2222-2222-2222-222222222222}.Release|x86.Build.0 = Release|Any CPU[m
 		{A1B2C3D4-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
 		{A1B2C3D4-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Debug|x86.Build.0 = Debug|Any CPU[m
 		{A1B2C3D4-3333-3333-3333-333333333333}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
 		{A1B2C3D4-3333-3333-3333-333333333333}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-3333-3333-3333-333333333333}.Release|x86.Build.0 = Release|Any CPU[m
 		{A1B2C3D4-4444-4444-4444-444444444444}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
 		{A1B2C3D4-4444-4444-4444-444444444444}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Debug|x86.Build.0 = Debug|Any CPU[m
 		{A1B2C3D4-4444-4444-4444-444444444444}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
 		{A1B2C3D4-4444-4444-4444-444444444444}.Release|Any CPU.Build.0 = Release|Any CPU[m
[31m-		{A1B2C3D4-5555-5555-5555-555555555555}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
[31m-		{A1B2C3D4-5555-5555-5555-555555555555}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[31m-		{A1B2C3D4-5555-5555-5555-555555555555}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
[31m-		{A1B2C3D4-5555-5555-5555-555555555555}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{A1B2C3D4-4444-4444-4444-444444444444}.Release|x86.Build.0 = Release|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Debug|x86.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C}.Release|x86.Build.0 = Release|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Debug|x86.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10}.Release|x86.Build.0 = Release|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Debug|Any CPU.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Debug|Any CPU.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Debug|x64.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Debug|x64.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Debug|x86.ActiveCfg = Debug|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Debug|x86.Build.0 = Debug|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Release|Any CPU.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Release|Any CPU.Build.0 = Release|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Release|x64.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Release|x64.Build.0 = Release|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Release|x86.ActiveCfg = Release|Any CPU[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081}.Release|x86.Build.0 = Release|Any CPU[m
[32m+[m	[32mEndGlobalSection[m
[32m+[m	[32mGlobalSection(SolutionProperties) = preSolution[m
[32m+[m		[32mHideSolutionNode = FALSE[m
 	EndGlobalSection[m
 	GlobalSection(NestedProjects) = preSolution[m
 		{A1B2C3D4-1111-1111-1111-111111111111} = {A1B2C3D4-0001-0000-0000-000000000000}[m
 		{A1B2C3D4-2222-2222-2222-222222222222} = {A1B2C3D4-0001-0000-0000-000000000000}[m
 		{A1B2C3D4-3333-3333-3333-333333333333} = {A1B2C3D4-0001-0000-0000-000000000000}[m
 		{A1B2C3D4-4444-4444-4444-444444444444} = {A1B2C3D4-0001-0000-0000-000000000000}[m
[31m-		{A1B2C3D4-5555-5555-5555-555555555555} = {A1B2C3D4-0001-0000-0000-000000000000}[m
[32m+[m		[32m{3C8CECB9-B7A6-49A5-BA1E-824AB00D917C} = {A1B2C3D4-0001-0000-0000-000000000000}[m
[32m+[m		[32m{132767F0-26E5-4C9E-8196-5F39C24B1D10} = {A1B2C3D4-0001-0000-0000-000000000000}[m
[32m+[m		[32m{E04EB041-8095-41EB-A53D-6B5488D1C081} = {A1B2C3D4-0001-0000-0000-000000000000}[m
 	EndGlobalSection[m
 EndGlobal[m
[1mdiff --git a/CLAUDE.md b/CLAUDE.md[m
[1mindex bd6a2f8..eeef5c8 100644[m
[1m--- a/CLAUDE.md[m
[1m+++ b/CLAUDE.md[m
[36m@@ -11,12 +11,48 @@[m [mdotnet build ApiLoader.sln[m
 # Build a specific project[m
 dotnet build src/Canal.Ingestion.ApiLoader/Canal.Ingestion.ApiLoader.csproj[m
 [m
[31m-# Run the host console application[m
[31m-dotnet run --project src/Canal.Ingestion.ApiLoader.Host/Canal.Ingestion.ApiLoader.Host.csproj[m
[32m+[m[32m# Run the TruckerCloud host[m
[32m+[m[32mdotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud[m
[32m+[m
[32m+[m[32m# Run the FMCSA host[m
[32m+[m[32mdotnet run --project src/Canal.Ingestion.ApiLoader.Host.Fmcsa[m
 ```[m
 [m
 Target framework is .NET 10.0 with nullable reference types and implicit usings enabled. There are no tests in the repository currently.[m
 [m
[32m+[m[32m### CLI Usage[m
[32m+[m
[32m+[m[32mEach vendor host provides a `System.CommandLine`-based CLI with two commands:[m
[32m+[m
[32m+[m[32m```bash[m
[32m+[m[32m# List all endpoints for a vendor[m
[32m+[m[32mdotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- list[m
[32m+[m[32mdotnet run --project src/Canal.Ingestion.ApiLoader.Host.Fmcsa -- list[m
[32m+[m
[32m+[m[32m# Load a specific endpoint (dependencies auto-resolved)[m
[32m+[m[32mdotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- load CarriersV4 --storage file[m
[32m+[m[32mdotnet run --project src/Canal.Ingestion.ApiLoader.Host.TruckerCloud -- load SafetyEventsV5 --dry-run[m
[32m+[m
[32m+[m[32m# Global options (apply to all commands)[m
[32m+[m[32m--environment, -e    Environment tag for storage path[m
[32m+[m[32m--storage, -s        Storage backend: adls | file[m
[32m+[m[32m--local-storage-path Root folder when --storage file[m
[32m+[m[32m--max-dop            Max parallel requests[m
[32m+[m[32m--max-retries        Max retries per request[m
[32m+[m
[32m+[m[32m# Always-present load options (apply to every load subcommand)[m
[32m+[m[32m--max-pages          Stop after N pages[m
[32m+[m[32m--save-behavior      PerPage | AfterAll | None[m
[32m+[m[32m--dry-run            Show execution plan without fetching[m
[32m+[m
[32m+[m[32m# Conditional load options (present only when endpoint metadata enables them)[m
[32m+[m[32m--page-size          Override default page size (if endpoint has DefaultPageSize)[m
[32m+[m[32m--start-utc          Start of time window (if endpoint supports watermark)[m
[32m+[m[32m--end-utc            End of time window (if endpoint supports watermark)[m
[32m+[m[32m--no-save-watermark  Skip saving watermark (if endpoint supports watermark)[m
[32m+[m[32m--body-params-json   JSON body for POST request (if endpoint uses POST)[m
[32m+[m[32m```[m
[32m+[m
 ## Architecture[m
 [m
 This is a vendor-agnostic API ingestion engine that fetches data from external APIs and persists it to Azure Data Lake Storage (ADLS) or local filesystem.[m
[36m@@ -24,7 +60,9 @@[m [mThis is a vendor-agnostic API ingestion engine that fetches data from external A[m
 ### Projects[m
 [m
 - **Canal.Ingestion.ApiLoader** — Core library: engine, models, adapters interface, storage interface[m
[31m-- **Canal.Ingestion.ApiLoader.Host** — Console app entry point; wires DI, configures endpoints, runs loads[m
[32m+[m[32m- **Canal.Ingestion.ApiLoader.Hosting** — Shared hosting library: `VendorHostBuilder`, CLI command builders, configuration, helpers[m
[32m+[m[32m- **Canal.Ingestion.ApiLoader.Host.TruckerCloud** — Thin Exe host for TruckerCloud vendor (~28 lines)[m
[32m+[m[32m- **Canal.Ingestion.ApiLoader.Host.Fmcsa** — Thin Exe host for FMCSA vendor (~20 lines)[m
 - **Canal.Ingestion.ApiLoader.TruckerCloud** — Vendor adapter for TruckerCloud API (authenticated, paginated)[m
 - **Canal.Ingestion.ApiLoader.Fmcsa** — Vendor adapter for FMCSA public transportation data API[m
 - **Canal.Storage.Adls** — Azure Blob Storage read/write utilities[m
[36m@@ -32,20 +70,37 @@[m [mThis is a vendor-agnostic API ingestion engine that fetches data from external A[m
 ### Dependency Graph[m
 [m
 ```[m
[31m-Host → Core ApiLoader → (references vendor adapters at host level)[m
[31m-TruckerCloud → Core ApiLoader[m
[31m-FMCSA → Core ApiLoader[m
[32m+[m[32mHost.TruckerCloud → Hosting + TruckerCloud adapter[m
[32m+[m[32mHost.Fmcsa → Hosting + Fmcsa adapter[m
[32m+[m[32mHosting → Core ApiLoader + Canal.Storage.Adls[m
[32m+[m[32mTruckerCloud adapter → Core ApiLoader[m
[32m+[m[32mFmcsa adapter → Core ApiLoader[m
 Core ApiLoader → Canal.Storage.Adls[m
 ```[m
 [m
[32m+[m[32m### Host Architecture[m
[32m+[m
[32m+[m[32mEach vendor host is a thin `Program.cs` that uses `VendorHostBuilder` (fluent builder pattern) to:[m
[32m+[m[32m1. Register the vendor name and adapter factory (`Func<HttpClient, ILoggerFactory, IVendorAdapter>`)[m
[32m+[m[32m2. Set the CLI executable name via `WithExecutableName()` (shown in help/usage output)[m
[32m+[m[32m3. Register the endpoint catalog (`IReadOnlyList<EndpointEntry>`)[m
[32m+[m[32m4. Optionally bind vendor-specific settings (e.g., `TruckerCloudSettings`)[m
[32m+[m[32m5. Load embedded `hostDefaults.json` defaults[m
[32m+[m
[32m+[m[32m`VendorHostBuilder.RunAsync(args)` builds the configuration stack (embedded defaults → `appsettings.json` → env vars → CLI args), constructs the `System.CommandLine` root command with `load` and `list` subcommands, and invokes it.[m
[32m+[m
[32m+[m[32mCLI options on `load` subcommands are **derived from endpoint metadata** — e.g., `--start-utc`/`--end-utc` only appear for endpoints where `SupportsWatermark == true`, `--body-params-json` only for POST endpoints.[m
[32m+[m
 ### Execution Pipeline[m
 [m
[31m-1. **Program.cs (Host)** — Configures HttpClient, vendor adapter, ingestion store, and endpoint list[m
[31m-2. **EndpointLoaderFactory** → creates **EndpointLoader** per endpoint[m
[31m-3. **EndpointLoader** — Orchestrates a load: manages time windows, watermarks, builds requests via `EndpointDefinition.BuildRequests`[m
[31m-4. **FetchEngine** — Executes HTTP requests with retry logic and configurable parallelism[m
[31m-5. **IVendorAdapter** — Applied at each step for vendor-specific URI construction, auth headers, response interpretation, pagination[m
[31m-6. **IIngestionStore** — Persists payloads and metadata (`AdlsIngestionStore` or `LocalFileIngestionStore`)[m
[32m+[m[32m1. **Program.cs (vendor host)** — Configures `VendorHostBuilder` with adapter factory and endpoint catalog[m
[32m+[m[32m2. **VendorHostBuilder** — Builds configuration, CLI commands, and infrastructure (store, HttpClient, adapter)[m
[32m+[m[32m3. **LoadCommandHandler** — Resolves dependency chain, auto-fetches dependencies, loads target endpoint[m
[32m+[m[32m4. **EndpointLoaderFactory** → creates **EndpointLoader** per endpoint[m
[32m+[m[32m5. **EndpointLoader** — Orchestrates a load: manages time windows, watermarks, builds requests via `EndpointDefinition.BuildRequests`[m
[32m+[m[32m6. **FetchEngine** — Executes HTTP requests with retry logic and configurable parallelism[m
[32m+[m[32m7. **IVendorAdapter** — Applied at each step for vendor-specific URI construction, auth headers, response interpretation, pagination[m
[32m+[m[32m8. **IIngestionStore** — Persists payloads and metadata (`AdlsIngestionStore` or `LocalFileIngestionStore`)[m
 [m
 ### Key Abstractions[m
 [m
[36m@@ -55,16 +110,25 @@[m [mCore ApiLoader → Canal.Storage.Adls[m
 - `AdlsIngestionStore` — Azure Blob Storage (production)[m
 - `LocalFileIngestionStore` — Local filesystem for dev without Azure credentials[m
 [m
[32m+[m[32m**EndpointDefinition** (`src/Canal.Ingestion.ApiLoader/Model/EndpointDefinition.cs`): Declarative metadata for an endpoint — resource name, version, HTTP method, page size, watermark support, time spans, `BuildRequests` delegate, `Description`, `DependsOn`.[m
[32m+[m
[32m+[m[32m**EndpointEntry** (`src/Canal.Ingestion.ApiLoader/Model/EndpointEntry.cs`): Pairs a CLI-friendly name with an `EndpointDefinition`. Vendor endpoint catalogs expose `static IReadOnlyList<EndpointEntry> All`.[m
[32m+[m
 **Request / FetchResult / FetchMetaData**: Core model chain. `Request` defines what to fetch; `FetchResult` captures the outcome (status, payload bytes, SHA256, pagination info); `FetchMetaData` serializes structured metadata to JSON with snake_case and selective field redaction.[m
 [m
 **RequestBuilders** (`src/Canal.Ingestion.ApiLoader/Engine/RequestBuilders.cs`): Factory methods for building request delegates — `Simple()`, `CarrierDependent()`, `CarrierAndTimeWindow()`.[m
 [m
 ### Adding a New Vendor[m
 [m
[31m-1. Create a new project referencing `Canal.Ingestion.ApiLoader`[m
[32m+[m[32m1. Create a new adapter project referencing `Canal.Ingestion.ApiLoader`[m
 2. Implement `IVendorAdapter` (or extend `VendorAdapterBase`)[m
[31m-3. Define endpoints as `EndpointDefinition` instances with appropriate `BuildRequests` delegates[m
[31m-4. Wire the adapter and endpoints in `Program.cs`[m
[32m+[m[32m3. Define endpoints as `EndpointDefinition` instances with `Description` and `DependsOn` metadata[m
[32m+[m[32m4. Add a `static IReadOnlyList<EndpointEntry> All` property to the endpoints class[m
[32m+[m[32m5. Create a new Exe project referencing `Canal.Ingestion.ApiLoader.Hosting` and the adapter project[m
[32m+[m[32m6. Write a `Program.cs` (~20-30 lines) using `VendorHostBuilder`; call `WithExecutableName()` to set the CLI executable name shown in help/usage[m
[32m+[m[32m7. Add embedded `hostDefaults.json` with default configuration[m
[32m+[m[32m   - The host `.csproj` must embed the file via `<EmbeddedResource Include="hostDefaults.json" />`[m
[32m+[m[32m   - `Program.cs` must load it via `Assembly.GetManifestResourceStream(...)` using the fully-qualified resource name (namespace + filename)[m
 [m
 ### Storage Path Convention[m
 [m
[36m@@ -76,7 +140,16 @@[m [mMetadata goes in a parallel `metadata/` subdirectory. Watermarks stored as `inge[m
 [m
 ### Configuration[m
 [m
[31m-Settings come from `appsettings.json` (git-ignored) under `AppSettings:` keys — API credentials, Azure storage account/container/tenant/client. The host reads these via `IConfiguration`.[m
[32m+[m[32mConfiguration is layered (last wins):[m
[32m+[m[32m1. Embedded `hostDefaults.json` in vendor host assembly[m
[32m+[m[32m2. `appsettings.json` in working directory (git-ignored)[m
[32m+[m[32m3. Environment variables[m
[32m+[m[32m4. CLI arguments (global options like `--environment`, `--storage`)[m
[32m+[m
[32m+[m[32mSettings are bound to:[m
[32m+[m[32m- `LoaderSettings` — shared loader config (environment, retries, DOP, storage backend)[m
[32m+[m[32m- `AzureSettings` — ADLS credentials (account, container, tenant, client ID/secret)[m
[32m+[m[32m- Vendor-specific settings (e.g., `TruckerCloudSettings` for API credentials)[m
 [m
 ### Retry Logic (FetchEngine)[m
 [m
[1mdiff --git a/src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs b/src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs[m
[1mindex 37dc01d..f04eb5e 100644[m
[1m--- a/src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs[m
[1m+++ b/src/Canal.Ingestion.ApiLoader.Fmcsa/FmcsaEndpoints.cs[m
[36m@@ -14,7 +14,8 @@[m [mpublic static class FmcsaEndpoints[m
     {[m
         ResourceName = "qh9u-swkp.json", FriendlyName = "ActPendInsurAllHistory", ResourceVersion = 1,[m
         DefaultPageSize = 500,[m
[31m-        BuildRequests = RequestBuilders.Simple[m
[32m+[m[32m        BuildRequests = RequestBuilders.Simple,[m
[32m+[m[32m        Description = "Active/pending insurance history."[m
     };[m
 [m
     /// <summary>Authority history.</summary>[m
[36m@@ -22,7 +23,8 @@[m [mpublic static class FmcsaEndpoints[m
     {[m
         ResourceName = "9mw4-x3tu.json", FriendlyName = "AuthHistoryAllHistory", ResourceVersion = 1,[m
         DefaultPageSize = 500,[m
[31m-        BuildRequests = RequestBuilders.Simple[m
[32m+[m[32m        BuildRequests = RequestBuilders.Simple,[m
[32m+[m[32m        Description = "Authority history."[m
     };[m
 [m
     /// <summary>BOC-3 process agent history.</summary>[m
[36m@@ -30,7 +32,8 @@[m [mpublic static class FmcsaEndpoints[m
     {[m
         ResourceName = "2emp-mxtb.json", FriendlyName = "Boc3AllHistory", ResourceVersion = 1,[m
         DefaultPageSize = 500,[m
[31m-        BuildRequests = RequestBuilders.Simple[m
[32m+[m[32m        BuildRequests = RequestBuilders.Simple,[m
[32m+[m[32m        Description = "BOC-3 process agent history."[m
     };[m
 [m
     /// <summary>Carrier registration history.</summary>[m
[36m@@ -38,7 +41,8 @@[m [mpublic static class FmcsaEndpoints[m
     {[m
         ResourceName = "6eyk-hxee.json", FriendlyName = "CarrierAllHistory", ResourceVersion = 1,[m
         DefaultPageSiz