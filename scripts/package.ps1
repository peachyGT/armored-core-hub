param([switch]$Offline,[switch]$UsePublishedFiles,[string]$InstallerCompiler,[string]$ArtifactDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$Version=([xml](Get-Content -LiteralPath Directory.Build.props -Raw)).Project.PropertyGroup.Version
$packageRoot=Join-Path $projectRoot "outputs/ArmoredCoreHub-$Version"
if(!$ArtifactDirectory){$ArtifactDirectory=Join-Path $projectRoot "outputs/release-$Version"}
$ArtifactDirectory=[IO.Path]::GetFullPath($ArtifactDirectory)
if((Test-Path -LiteralPath $packageRoot) -and !$UsePublishedFiles){throw 'Use a fresh publish directory, or explicitly select -UsePublishedFiles to audit and package an existing clean publish.'}
if(Test-Path -LiteralPath $ArtifactDirectory){throw 'Release artifacts directory already exists; preserve it and choose a new directory.'}
if(!$UsePublishedFiles){& (Join-Path $PSScriptRoot 'build.ps1') -Test -Publish -Offline:$Offline -OutputDirectory $packageRoot}
$audit=& (Join-Path $PSScriptRoot 'audit-release.ps1') -PackageDirectory $packageRoot
if($audit.version.Split('+')[0] -ne $Version){throw 'Published app version does not match Directory.Build.props'}
$audit | ConvertTo-Json | Write-Output
New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
$zip=Join-Path $ArtifactDirectory 'ArmoredCoreHub-win-x64.zip'
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip
if($InstallerCompiler){
    $compiler=(Resolve-Path -LiteralPath $InstallerCompiler).Path
    & $compiler /Q "/DAppVersion=$Version" "/DPackageDir=$packageRoot" "/DArtifactDir=$ArtifactDirectory" (Join-Path $projectRoot 'installer/ArmoredCoreHub.iss')
    if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
}
$hashLines=foreach($file in Get-ChildItem -LiteralPath $ArtifactDirectory -File | Sort-Object Name){
    $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
[IO.File]::WriteAllLines((Join-Path $ArtifactDirectory 'SHA256SUMS.txt'),$hashLines,[Text.Encoding]::ASCII)
Get-ChildItem -LiteralPath $ArtifactDirectory -File | Select-Object Name,Length
