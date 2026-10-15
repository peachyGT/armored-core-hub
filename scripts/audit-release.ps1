param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
$packageRoot=(Resolve-Path -LiteralPath $PackageDirectory).Path
$allowedDocs=@('docs/usage.md','docs/compatibility.md','docs/artwork-credits.md','docs/saves-and-controls.md','docs/updates.md')
$required=@('ArmoredCoreHub.exe','ArmoredCoreHub.dll','Hub.Core.dll','coreclr.dll','manifests/catalog.json','manifests/covers.json','START-HERE.txt','LEGAL.md','LICENSE','THIRD-PARTY-NOTICES.md','third-party/7zip/7zr.exe')+$allowedDocs
$rootNotices=@('START-HERE.txt','LEGAL.md','LICENSE','THIRD-PARTY-NOTICES.md')
$cultures=@([Globalization.CultureInfo]::GetCultures([Globalization.CultureTypes]::AllCultures) | ForEach-Object Name)
$fileCount=0;$bytes=0L
foreach($entry in Get-ChildItem -LiteralPath $packageRoot -Recurse -Force){
    if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Linked entry in release: $($entry.Name)"}
    $relative=[IO.Path]::GetRelativePath($packageRoot,$entry.FullName).Replace('\','/')
    if($entry.PSIsContainer){
        if($relative -notin @('docs','manifests','third-party','third-party/7zip') -and $relative -notin $cultures){throw "Unexpected release directory: $relative"}
        continue
    }
    $fileCount++;$bytes+=$entry.Length
    if($relative.StartsWith('docs/')){if($relative -notin $allowedDocs){throw "Development documentation in release: $relative"};continue}
    if($relative.StartsWith('third-party/')){if($entry.Extension -ne '.txt' -and $relative -ne 'third-party/7zip/7zr.exe'){throw "Unexpected component file: $relative"};continue}
    if($relative.StartsWith('manifests/')){if($relative -notin @('manifests/catalog.json','manifests/covers.json')){throw "Unexpected manifest: $relative"};continue}
    if($relative.Contains('/')){
        $parts=$relative.Split('/')
        if($parts.Count -eq 2 -and $parts[0] -in $cultures -and $parts[1].EndsWith('.resources.dll')){continue}
        throw "Unexpected release path: $relative"
    }
    if($relative -notin $rootNotices -and $entry.Extension -notin @('.dll','.exe','.json')){throw "Unexpected release file: $relative"}
    if($entry.Extension -eq '.exe' -and $entry.Name -notin @('ArmoredCoreHub.exe','createdump.exe')){throw "Unexpected executable: $relative"}
}
foreach($relative in $required){if(!(Test-Path -LiteralPath (Join-Path $packageRoot $relative) -PathType Leaf)){throw "Missing runtime or user file: $relative"}}
$assembly=[Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $packageRoot 'ArmoredCoreHub.dll')))
foreach($method in @('SelfTest','CheckSmallFeatures','CheckGraphicsDrafts','CheckAchievementAccount','CheckResources')){if($assembly.Contains($method)){throw "Diagnostic test method in shipping assembly: $method"}}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $packageRoot 'ArmoredCoreHub.exe')).ProductVersion
[pscustomobject]@{passed=$true;files=$fileCount;bytes=$bytes;version=$version;developmentFiles=$false;diagnosticMethods=$false}
