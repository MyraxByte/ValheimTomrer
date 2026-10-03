# Build ValheimTomrer, deploy it, launch Valheim, and stream our log lines (Windows).
#   .\scripts\dev.ps1            normal run
#   .\scripts\dev.ps1 -Debug     also open the Mono soft debugger on 127.0.0.1:10000
#   .\scripts\dev.ps1 -Hot       deploy into BepInEx\scripts so ScriptEngine reloads it (see docs\development.md)
param([switch]$Debug, [switch]$Hot)
$ErrorActionPreference = "Stop"

$Repo = Split-Path -Parent $PSScriptRoot
$Valheim = if ($env:VALHEIM_INSTALL) { $env:VALHEIM_INSTALL } else { Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Valheim" }
$Exe = Join-Path $Valheim "valheim.exe"
$Log = Join-Path $Valheim "BepInEx\LogOutput.log"
if (-not (Test-Path $Exe)) { throw "valheim.exe not found in $Valheim. Set VALHEIM_INSTALL." }

$GameArgs = @()
if ($Debug) {
  # This doorstop flag takes a value. Passing it bare aborts the start.
  $GameArgs += @("--doorstop-mono-debug-enabled", "true")
  Write-Host "==> Mono debugger will listen on 127.0.0.1:10000"
}

Write-Host "==> building"
dotnet build "$Repo\ValheimTomrer.csproj" -v minimal -nologo "-p:HotReload=$($Hot.ToString().ToLower())"
if ($LASTEXITCODE -ne 0) { throw "build failed" }

if (Get-Process valheim -ErrorAction SilentlyContinue) { throw "Valheim is already running. Close it first." }

Write-Host "==> launching Valheim"
Remove-Item $Log -ErrorAction SilentlyContinue
$Game = Start-Process -FilePath $Exe -ArgumentList $GameArgs -WorkingDirectory $Valheim -PassThru

Write-Host "==> waiting for BepInEx"
for ($i = 0; $i -lt 90 -and -not (Test-Path $Log); $i++) { Start-Sleep -Seconds 1 }
if (-not (Test-Path $Log)) {
  Write-Host "!! BepInEx never wrote a log. Is BepInExPack_Valheim installed (winhttp.dll and doorstop_config.ini in the game folder)?"
  exit 1
}

Write-Host "==> streaming ValheimTomrer and errors (Ctrl-C to stop watching, the game keeps running)"
Get-Content $Log -Wait -Tail 0 | Where-Object { $_ -match "ValheimTomrer|Error|Exception|Harmony" }
