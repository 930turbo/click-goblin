$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
New-Item -ItemType Directory -Force -Path "$repoRoot\dist" | Out-Null
$sources = @(Get-ChildItem -LiteralPath "$repoRoot\src\ClickGoblin" -Filter *.cs | ForEach-Object FullName)
$resources = @(
    "/resource:$repoRoot\assets\fonts\Outfit-Regular.ttf,Outfit-Regular.ttf",
    "/resource:$repoRoot\assets\fonts\Outfit-SemiBold.ttf,Outfit-SemiBold.ttf",
    "/resource:$repoRoot\assets\sounds\start.wav,start.wav",
    "/resource:$repoRoot\assets\sounds\stop.wav,stop.wav",
    "/resource:$repoRoot\assets\fonts\OFL.txt,Outfit-OFL.txt",
    "/resource:$repoRoot\LICENSE,ClickGoblin-LICENSE.txt"
)
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu "/win32manifest:$repoRoot\assets\app.manifest" "/win32icon:$repoRoot\assets\click-goblin.ico" "/out:$repoRoot\dist\ClickGoblin.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll $resources $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close Click Goblin if the executable is in use.' }
Write-Output 'Built dist\ClickGoblin.exe'
