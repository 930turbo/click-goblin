param([switch]$SkipUi)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
New-Item -ItemType Directory -Force -Path "$repoRoot\test-output" | Out-Null
$sources = @(Get-ChildItem -LiteralPath "$repoRoot\src\ClickGoblin" -Filter *.cs | ForEach-Object FullName)
& $compiler /nologo /target:exe /main:ClickGoblin.Tests /optimize+ "/win32icon:$repoRoot\assets\click-goblin.ico" "/out:$repoRoot\test-output\Tests.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll $sources "$repoRoot\tests\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$testArgs = @("$repoRoot\test-output")
if ($SkipUi) { $testArgs += "--skip-ui" }
& "$repoRoot\test-output\Tests.exe" @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
