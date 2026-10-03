# Builds the Release DLL and packs the Thunderstore zip: thunderstore\build\ValheimTomrer.zip (Windows).
# Same checks as scripts/zip.sh. Every run makes the zip from scratch and does not copy the DLL into the game.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Repo = Split-Path -Parent $PSScriptRoot
$Out = Join-Path $Repo "thunderstore\build"
$Zip = Join-Path $Out "ValheimTomrer.zip"
$Dll = Join-Path $Repo "bin\Release\ValheimTomrer.dll"
$Manifest = Join-Path $Repo "thunderstore\manifest.json"
$Icon = Join-Path $Repo "thunderstore\icon.png"
function Fail($m) { Write-Host "!! $m"; exit 1 }

Write-Host "==> building Release"
dotnet build "$Repo\ValheimTomrer.csproj" -c Release -v minimal -nologo -p:DeployToGame=false
if ($LASTEXITCODE -ne 0) { Fail "build failed" }

Write-Host "==> checking"
$Files = @($Manifest, $Icon, (Join-Path $Repo "README.md"), (Join-Path $Repo "CHANGELOG.md"), (Join-Path $Repo "LICENSE"), $Dll)
foreach ($f in $Files) { if (-not (Test-Path $f)) { Fail "missing $f" } }

$m = Get-Content $Manifest -Raw | ConvertFrom-Json
foreach ($k in "name", "version_number", "website_url", "description", "dependencies") {
  if (-not $m.PSObject.Properties[$k]) { Fail "manifest.json has no $k" }
}
if ($m.name -notmatch '^[A-Za-z0-9_]{1,128}$') { Fail "bad name: $($m.name)" }
if ($m.version_number -notmatch '^\d+\.\d+\.\d+$') { Fail "bad version: $($m.version_number)" }
if ($m.description.Length -gt 250) { Fail "description is $($m.description.Length) characters, 250 at most" }

$csproj = [regex]::Match((Get-Content "$Repo\ValheimTomrer.csproj" -Raw), '<Version>(.*?)</Version>').Groups[1].Value
$plugin = [regex]::Match((Get-Content "$Repo\src\Plugin.cs" -Raw), 'PluginVersion = "(.*?)"').Groups[1].Value
if ($m.version_number -ne $csproj -or $m.version_number -ne $plugin) {
  Fail "versions differ: manifest $($m.version_number), csproj $csproj, Plugin.cs $plugin"
}

# icon.png must be 256 x 256: width and height are big-endian ints at bytes 16 and 20 of a PNG
$b = [System.IO.File]::ReadAllBytes($Icon)
$w = ($b[16] -shl 24) + ($b[17] -shl 16) + ($b[18] -shl 8) + $b[19]
$h = ($b[20] -shl 24) + ($b[21] -shl 16) + ($b[22] -shl 8) + $b[23]
if ($w -ne 256 -or $h -ne 256) { Fail "icon.png is $w x $h, it must be 256 x 256" }

# A Release build leaves the autotest out (src\Dev is Debug only).
$text = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($Dll))
if ($text.Contains("AutoTestPeace")) { Fail "the Release DLL still has the autotest in it" }

Write-Host "==> packing $($m.version_number)"
New-Item -ItemType Directory -Force $Out | Out-Null
Remove-Item $Zip -ErrorAction SilentlyContinue
$archive = [System.IO.Compression.ZipFile]::Open($Zip, "Create")
try {
  # The files go at the zip's root, not in a folder.
  foreach ($f in $Files) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f, (Split-Path $f -Leaf)) | Out-Null
  }
} finally { $archive.Dispose() }
$z = [System.IO.Compression.ZipFile]::OpenRead($Zip)
try { $z.Entries | ForEach-Object { Write-Host ("{0,10}  {1}" -f $_.Length, $_.FullName) } } finally { $z.Dispose() }
Write-Host "==> $Zip"
