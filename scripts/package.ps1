param([switch]$Offline,[string]$Version='0.3.10')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$packageRoot=Join-Path $projectRoot "outputs/ArmoredCoreHub-$Version"
# Never package a user's previously used portable directory.
foreach($data in @('config','games','saves','firmware','runtime','cache','logs','patches','extensions','artwork','achievements')){
    if(Test-Path -LiteralPath (Join-Path $packageRoot $data)){throw "Package folder contains user/runtime data: $data. Choose a clean output directory first."}
}
& (Join-Path $PSScriptRoot 'build.ps1') -Test -Publish -Offline:$Offline -OutputDirectory $packageRoot
if(!(Test-Path -LiteralPath (Join-Path $packageRoot 'ArmoredCoreHub.exe'))){throw 'No application to package'}
& (Join-Path $PSScriptRoot 'validate-portable.ps1') -PackageDirectory $packageRoot
$zip=Join-Path $projectRoot "outputs/ArmoredCoreHub-$Version-win-x64.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Select-Object Hash,@{Name='File';Expression={Split-Path $_.Path -Leaf}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot "outputs/ArmoredCoreHub-$Version-win-x64.sha256.json")
