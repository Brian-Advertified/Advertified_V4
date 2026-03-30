$root = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:APPDATA = Join-Path $root ".artifacts\local-api-env\AppData"
$env:LOCALAPPDATA = Join-Path $root ".artifacts\local-api-env\LocalAppData"

New-Item -ItemType Directory -Force -Path $env:APPDATA, $env:LOCALAPPDATA | Out-Null

Set-Location $root
dotnet run --project src/Advertified.App/Advertified.App.csproj
