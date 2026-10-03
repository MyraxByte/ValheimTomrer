# Runs a scripted test session in Valheim (src\Dev\AutoTest.cs, Debug builds only) and prints the results (Windows).
# Same scenarios as scripts/autotest.sh (editor_redesign included), see the list there and in CLAUDE.md. It uses its own character, world and
# save folder, never the player's saves. Output goes to .devtest\ in the repo.
#   .\scripts\autotest.ps1                 scenario "blueprints"
#   .\scripts\autotest.ps1 editor_all      everything, then the art guard (about 11 minutes). The one to run.
#   $env:VT_CHAIN = "editor_build,blueprints"; .\scripts\autotest.ps1 editor_all    only those, in that order
param([string]$Scenario = "blueprints")
$ErrorActionPreference = "Stop"

$Repo = Split-Path -Parent $PSScriptRoot
# One source of truth for the game folder: MSBuild (-p, VALHEIM_INSTALL, Directory.Build.props.user, the default)
$Valheim = (dotnet msbuild "$Repo\ValheimTomrer.csproj" -getProperty:ValheimInstall -nologo).Trim()
$Timeout = if ($env:AUTOTEST_TIMEOUT) { [int]$env:AUTOTEST_TIMEOUT } elseif ($Scenario -eq "editor_all") { 1800 } else { 600 }
$Out = Join-Path $Repo ".devtest"
$Log = Join-Path $Valheim "BepInEx\LogOutput.log"

if (Get-Process valheim -ErrorAction SilentlyContinue) { throw "Valheim is already running. Close it first." }

Write-Host "==> building (Debug, into BepInEx\plugins)"
dotnet build "$Repo\ValheimTomrer.csproj" -c Debug -v minimal -nologo -p:HotReload=false
if ($LASTEXITCODE -ne 0) { throw "build failed" }

New-Item -ItemType Directory -Force $Out | Out-Null
Remove-Item "$Out\*.png", "$Out\result.txt", "$Out\LogOutput.log" -ErrorAction SilentlyContinue

Write-Host "==> launching Valheim (scenario: $Scenario, timeout ${Timeout}s)"
Remove-Item $Log -ErrorAction SilentlyContinue
$env:VT_AUTOTEST = $Scenario
$env:VT_OUTDIR = $Out
$Game = Start-Process -FilePath (Join-Path $Valheim "valheim.exe") -WorkingDirectory $Valheim -PassThru

try {
  for ($i = 0; $i -lt $Timeout; $i++) {
    if (Test-Path "$Out\result.txt") { break }
    if ($i -gt 30 -and $Game.HasExited) { Write-Host "!! the game closed before the test finished"; break }
    Start-Sleep -Seconds 1
  }
  Start-Sleep -Seconds 3
  Copy-Item $Log "$Out\LogOutput.log" -ErrorAction SilentlyContinue
}
finally {
  Get-Process valheim -ErrorAction SilentlyContinue | Stop-Process -Force
}

if (Test-Path "$Out\LogOutput.log") {
  Select-String -Path "$Out\LogOutput.log" -Pattern "AUTOTEST|Exception" | ForEach-Object { $_.Line }
} else { Write-Host "!! no BepInEx log (is BepInEx installed? see docs\development.md)" }

if (-not (Test-Path "$Out\result.txt")) {
  Write-Host "!! no result: timeout or crash. Full log: $Out\LogOutput.log"
  exit 1
}
Get-Content "$Out\result.txt"

if ($Scenario -eq "editor_all") {
  node "$Repo\scripts\check-rules.mjs"
  if ($LASTEXITCODE -ne 0) { exit 1 }
  Write-Host "art guard: no image, mesh, material or bundle file in the repo"
}

if (-not (Select-String -Path "$Out\result.txt" -Pattern "fail=0" -Quiet)) { exit 1 }
