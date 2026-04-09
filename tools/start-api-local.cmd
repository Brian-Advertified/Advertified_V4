@echo off
setlocal

set "ROOT=%~dp0.."
set "DOTNET_CLI_HOME=%ROOT%\.dotnet"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "APPDATA=%ROOT%\.artifacts\local-api-env\AppData"
set "LOCALAPPDATA=%ROOT%\.artifacts\local-api-env\LocalAppData"

if not exist "%APPDATA%" mkdir "%APPDATA%"
if not exist "%LOCALAPPDATA%" mkdir "%LOCALAPPDATA%"

cd /d "%ROOT%"
dotnet run --project src\Advertified.App\Advertified.App.csproj
pause
