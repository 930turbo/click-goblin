$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path $repoRoot 'release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
if (-not (Test-Path -LiteralPath "$repoRoot\dist\ClickGoblin.exe")) { throw 'Run scripts/build.ps1 first.' }
Copy-Item -LiteralPath "$repoRoot\dist\ClickGoblin.exe" -Destination "$releaseDir\ClickGoblin.exe"
Compress-Archive -LiteralPath "$repoRoot\dist\ClickGoblin.exe", "$repoRoot\docs\USAGE.md", "$repoRoot\LICENSE" -DestinationPath "$releaseDir\ClickGoblin-win.zip" -Force
$checksums = foreach ($name in @('ClickGoblin.exe', 'ClickGoblin-win.zip')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $releaseDir $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $releaseDir 'SHA256SUMS.txt'), [string[]]$checksums, [Text.Encoding]::ASCII)
Write-Output "Packaged release files in $releaseDir"
