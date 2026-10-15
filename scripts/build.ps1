param([switch]$Publish,[switch]$Test,[switch]$Offline,[switch]$Diagnostics,[string]$OutputDirectory='outputs/ArmoredCoreHub')
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
$diagnosticValue=if($Diagnostics){'true'}else{'false'}
& $dotnet build src/Hub.App/Hub.App.csproj -c Release --nologo "-p:HubDiagnostics=$diagnosticValue" @restoreArgs
if($LASTEXITCODE -ne 0){throw 'Application build failed'}
if($Test){& $dotnet run --project tests/Hub.Tests -c Release @restoreArgs -- work/test-results; if($LASTEXITCODE -ne 0){throw 'Tests failed'}}
if($Publish){
    if(Test-Path -LiteralPath $OutputDirectory){throw 'Publish into a new, empty directory to keep old or personal files out of the package.'}
    & $dotnet publish src/Hub.App/Hub.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false "-p:HubDiagnostics=$diagnosticValue" -o $OutputDirectory --nologo @restoreArgs
    if($LASTEXITCODE -ne 0){throw 'Publish failed'}
    $documentFiles=@('START-HERE.txt','LEGAL.md','LICENSE','THIRD-PARTY-NOTICES.md','docs/usage.md','docs/compatibility.md','docs/artwork-credits.md','docs/saves-and-controls.md','docs/updates.md')
    foreach($relative in $documentFiles){
        $destination=Join-Path $OutputDirectory $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $relative -Destination $destination
    }
    foreach($name in @('Barlow-OFL.txt','Cinzel-OFL.txt','SharpCompress-LICENSE.txt','WPF-LICENSE.txt','Xenia-LICENSE.txt','dotnet-LICENSE.txt','dotnet-ThirdPartyNotices.txt')){
        Copy-Item -LiteralPath (Join-Path 'third-party' $name) -Destination (Join-Path $OutputDirectory 'third-party')
    }
}
