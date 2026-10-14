$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\dist" | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu "/win32manifest:$PSScriptRoot\assets\app.manifest" "/win32icon:$PSScriptRoot\assets\ClickGoblin.ico" "/out:$PSScriptRoot\dist\ClickGoblin.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\src\UI.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close Click Goblin if the executable is in use.' }
Write-Output 'Built dist\ClickGoblin.exe'
