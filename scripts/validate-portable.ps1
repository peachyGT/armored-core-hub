param([string]$PackageDirectory='outputs/ArmoredCoreHub-0.3.10')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$source=[IO.Path]::GetFullPath($PackageDirectory)
$validationRoot=Join-Path $projectRoot ('work/portable-check-'+[Guid]::NewGuid().ToString('N'))
$reportPath=Join-Path $projectRoot 'work/portable-results.json'
$results=@()
# Invalidate any earlier pass before starting: a failed run must never leave a
# successful report from another binary looking like this run's result.
New-Item -ItemType Directory -Path (Split-Path $reportPath -Parent) -Force | Out-Null
@{passed=$false;status='running';packageDirectory=$source;validationRoot=$validationRoot} | ConvertTo-Json | Set-Content -LiteralPath $reportPath
try {
if(!(Test-Path -LiteralPath (Join-Path $source 'ArmoredCoreHub.exe'))){throw 'Publish the app before validating it'}
New-Item -ItemType Directory -Path $validationRoot | Out-Null
$first=Join-Path $validationRoot 'Original Hub'
$second=Join-Path $validationRoot 'Moved Hub'
Copy-Item -LiteralPath $source -Destination $first -Recurse
function Invoke-HubCheck([string]$appRoot,[string]$name){
    $capture=Join-Path $validationRoot $name
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $process=Start-Process -FilePath (Join-Path $appRoot 'ArmoredCoreHub.exe') -ArgumentList @('--self-test','--capture',('"'+$capture+'"')) -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
    $runtime='unavailable'
    try {
        if($process.WaitForInputIdle(10000)){
            $module=$process.Modules | Where-Object {$_.ModuleName -eq 'coreclr.dll'} | Select-Object -First 1
            if($module){$runtime=if($module.FileName.StartsWith($appRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){'package-local'}else{'external'}}
        }
    } catch { $runtime='inspection unavailable' }
    if(!$process.WaitForExit(45000)){$process.Kill();throw 'Owned Hub validation process timed out'}
    $report=Get-Content -LiteralPath (Join-Path $capture 'ui-results.json') -Raw | ConvertFrom-Json
    if(!$report.passed){throw ($report | ConvertTo-Json -Depth 5)}
    if($runtime -ne 'package-local'){throw "Self-contained runtime not established: $runtime"}
    $timer.Stop()
    return [pscustomobject]@{phase=$name;passed=$true;runtime=$runtime;uiChecks=$report.checks.Count;elapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2)}
}
$results+=Invoke-HubCheck $first 'clean-start'
$sentinel=Join-Path $first 'saves/portable-test.txt'
Set-Content -LiteralPath $sentinel -Value 'Synthetic preservation marker'
$stateHash=(Get-FileHash -LiteralPath (Join-Path $first 'config/state.json') -Algorithm SHA256).Hash
$appearanceHash=(Get-FileHash -LiteralPath (Join-Path $first 'config/appearance.json') -Algorithm SHA256).Hash
# Both exact resolved move targets must stay in this generated validation workspace.
foreach($target in @($first,$second)){
    $resolved=[IO.Path]::GetFullPath($target)
    if(!$resolved.StartsWith([IO.Path]::GetFullPath($validationRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing an out-of-workspace move'}
}
Move-Item -LiteralPath $first -Destination $second
if((Get-FileHash -LiteralPath (Join-Path $second 'config/state.json') -Algorithm SHA256).Hash -ne $stateHash){throw 'Configuration changed during relocation'}
if((Get-FileHash -LiteralPath (Join-Path $second 'config/appearance.json') -Algorithm SHA256).Hash -ne $appearanceHash){throw 'Appearance preferences changed during relocation'}
$results+=Invoke-HubCheck $second 'relocated-start'
if((Get-Content -LiteralPath (Join-Path $second 'saves/portable-test.txt') -Raw).Trim() -ne 'Synthetic preservation marker'){throw 'Save marker changed'}
$report=[pscustomobject]@{passed=$true;packageDirectory=$source;validationRoot=$validationRoot;phases=$results;configurationPreserved=$true;appearancePreserved=$true;saveMarkerPreserved=$true;isolatedDataRoot=$true;freshWindowsVm=$false}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath
$report | ConvertTo-Json -Depth 8
} catch {
    @{passed=$false;status='failed';packageDirectory=$source;validationRoot=$validationRoot;phases=$results;error=$_.Exception.Message} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath
    throw
}
