param([switch]$Publish,[switch]$Test,[switch]$Offline,[string]$OutputDirectory='outputs/ArmoredCoreHub-0.3.10')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$env:DOTNET_CLI_HOME=Join-Path $projectRoot 'work/dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:NUGET_PACKAGES=Join-Path $projectRoot 'work/nuget'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$dotnet=Join-Path $projectRoot 'work/sdk/dotnet.exe'
if(!(Test-Path $dotnet)){$dotnet='dotnet'}
$restoreArgs=@()
if($Offline){$restoreArgs+='--no-restore'}
& $dotnet build src/Hub.App/Hub.App.csproj -c Release --nologo @restoreArgs
if($LASTEXITCODE -ne 0){throw 'Application build failed'}
if($Test){& $dotnet run --project tests/Hub.Tests -c Release @restoreArgs -- work/test-results; if($LASTEXITCODE -ne 0){throw 'Tests failed'}}
if($Publish){
    & $dotnet publish src/Hub.App/Hub.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $OutputDirectory --nologo @restoreArgs
    if($LASTEXITCODE -ne 0){throw 'Publish failed'}
    foreach($file in @('README.md','LEGAL.md','LICENSE','THIRD-PARTY-NOTICES.md','PRODUCT.md','DESIGN.md','STATUS.md','BLOCKED.md','CONTRIBUTING.md')){Copy-Item -LiteralPath $file -Destination $OutputDirectory}
    Copy-Item -LiteralPath docs -Destination $OutputDirectory -Recurse -Force
    Copy-Item -LiteralPath third-party -Destination $OutputDirectory -Recurse -Force
}
