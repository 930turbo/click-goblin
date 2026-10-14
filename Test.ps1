$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\test-output" | Out-Null
& $compiler /nologo /target:exe /main:ClickGoblin.Tests /optimize+ "/win32icon:$PSScriptRoot\assets\ClickGoblin.ico" "/out:$PSScriptRoot\test-output\Tests.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\src\UI.cs" "$PSScriptRoot\tests\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$PSScriptRoot\test-output\Tests.exe" "$PSScriptRoot\test-output"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
