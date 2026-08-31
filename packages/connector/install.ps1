# Mosaic Cloud - customer setup for Windows.
#
#   powershell -ExecutionPolicy Bypass -File .\install.ps1 -Url wss://cloud.example.com/tunnel -Token <token> [-Project <unity project path>]
#
# Installs Node if missing, adds the Unity package to the project, and starts the
# connector. Nothing of the Mosaic pipeline is installed on this machine.
param(
  [Parameter(Mandatory=$true)][string]$Url,
  [Parameter(Mandatory=$true)][string]$Token,
  [string]$Project
)
$ErrorActionPreference = "Stop"

Write-Host "== 1/3 checking the toolchain ==" -ForegroundColor Cyan
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  Write-Host "installing Node.js LTS via winget..." -ForegroundColor Yellow
  winget install --id OpenJS.NodeJS.LTS -e --accept-source-agreements --accept-package-agreements --silent | Out-Null
  $env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
              [Environment]::GetEnvironmentVariable("Path","User") + ";" + $env:Path
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  Write-Host "Node is still not on PATH. Open a NEW terminal and run this script again." -ForegroundColor Red
  exit 1
}
$major = [int]((node --version) -replace "^v(\d+).*", '$1')
if ($major -lt 20) { Write-Host "Node 20 or newer is required (found $(node --version))." -ForegroundColor Red; exit 1 }
Write-Host ("ok  node " + (node --version)) -ForegroundColor Green

Write-Host "== 2/3 Unity packages ==" -ForegroundColor Cyan
if ($Project) {
  $manifest = Join-Path $Project "Packages\manifest.json"
  if (-not (Test-Path $manifest)) { Write-Host "not a Unity project: $Project" -ForegroundColor Red; exit 1 }
  $m = Get-Content $manifest -Raw | ConvertFrom-Json
  $pkg = "com.mosaic.bridge"
  $src = "https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge"
  if (-not $m.dependencies.PSObject.Properties.Name.Contains($pkg)) {
    $m.dependencies | Add-Member -NotePropertyName $pkg -NotePropertyValue $src
    ($m | ConvertTo-Json -Depth 20) | Set-Content $manifest -Encoding UTF8
    Write-Host "added the Mosaic Bridge package to the project manifest" -ForegroundColor Green
  } else {
    Write-Host "the project already references the Mosaic Bridge package" -ForegroundColor Green
  }
  Write-Host "Open the project in Unity once so the package imports, then leave the Editor running."
} else {
  Write-Host "skipped (no -Project given). Re-run with -Project <path> to add the package automatically."
}

Write-Host "== 3/3 starting the connector ==" -ForegroundColor Cyan
$dir = $PSScriptRoot
if (-not (Test-Path (Join-Path $dir "node_modules"))) { Push-Location $dir; npm install --omit=dev | Out-Null; Pop-Location }
if (-not (Test-Path (Join-Path $dir "dist\index.js"))) { Push-Location $dir; npm install | Out-Null; npx tsc | Out-Null; Pop-Location }
Write-Host "Connecting to $Url"
Write-Host "Leave this window open while you work. Ctrl+C stops it."
node (Join-Path $dir "dist\index.js") --url $Url --token $Token
