# Compile Pépin en exe natif (NativeAOT) dans .\publish\Pepin.exe
# Prérequis : SDK .NET 10 + Visual Studio (charge de travail "Développement Desktop en C++").
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# L'éditeur de liens NativeAOT a besoin de vswhere.exe dans le PATH
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $vsInstaller) { $env:PATH = "$vsInstaller;$env:PATH" }

Get-Process Pepin -ErrorAction SilentlyContinue | Stop-Process
dotnet publish -c Release -r win-x64 -nologo -v q -o publish -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "échec de la compilation" }
Get-Item .\publish\Pepin.exe | Select-Object Name, @{ n = 'Taille (Ko)'; e = { [math]::Round($_.Length / 1KB) } }
