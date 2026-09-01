# Resets this machine to a pre-Mosaic state, so the first-run path can be tested again.
#
#   powershell -ExecutionPolicy Bypass -File .\reset-windows.ps1                 # what it would remove
#   powershell -ExecutionPolicy Bypass -File .\reset-windows.ps1 -Apply          # remove it
#   powershell -ExecutionPolicy Bypass -File .\reset-windows.ps1 -Apply -Project "C:\path\to\project"
#
# Removes only what Mosaic put there: the connector's config, the MCP entries in
# Claude's config, and the bridge package from a project's manifest plus its cache.
# Unity, Node, Git and the Editor itself are left alone.
param([switch]$Apply, [string]$Project)

$ErrorActionPreference = "Continue"
$mode = if ($Apply) { "REMOVING" } else { "DRY RUN - nothing will be changed" }
Write-Host "== Mosaic reset ($mode) ==" -ForegroundColor Cyan

function Act($what, $action) {
  Write-Host ("  " + $what) -ForegroundColor Yellow
  if ($Apply) { & $action }
}

# 1. Connector configuration and stored access code
$cfg = Join-Path $env:APPDATA "Mosaic"
if (Test-Path $cfg) { Act "delete $cfg" { Remove-Item -Recurse -Force $cfg } }
else { Write-Host "  (no connector config)" -ForegroundColor DarkGray }

# 2. Running connector
$procs = Get-Process -Name "mosaic-connector","index-win-x64" -ErrorAction SilentlyContinue
if ($procs) { Act "stop $($procs.Count) running connector process(es)" { $procs | Stop-Process -Force } }
else { Write-Host "  (no connector running)" -ForegroundColor DarkGray }

# 3. Claude's MCP entries, at every scope, without touching anything else in the file
$claude = Join-Path $env:USERPROFILE ".claude.json"
if (Test-Path $claude) {
  $json = Get-Content $claude -Raw | ConvertFrom-Json
  $hits = @()
  if ($json.mcpServers -and $json.mcpServers.PSObject.Properties.Name -contains "mosaic") { $hits += "user scope" }
  if ($json.projects) {
    foreach ($p in $json.projects.PSObject.Properties) {
      if ($p.Value.mcpServers -and $p.Value.mcpServers.PSObject.Properties.Name -contains "mosaic") { $hits += $p.Name }
    }
  }
  if ($hits.Count) {
    Act "remove the 'mosaic' server from .claude.json ($($hits -join '; '))" {
      if ($json.mcpServers) { $json.mcpServers.PSObject.Properties.Remove("mosaic") }
      if ($json.projects) {
        foreach ($p in $json.projects.PSObject.Properties) {
          if ($p.Value.mcpServers) { $p.Value.mcpServers.PSObject.Properties.Remove("mosaic") }
        }
      }
      ($json | ConvertTo-Json -Depth 40) | Set-Content $claude -Encoding UTF8
    }
  } else { Write-Host "  (no mosaic entry in .claude.json)" -ForegroundColor DarkGray }
}

# 4. The Unity project: package reference, lock entry, cache, and any .mcp.json the
#    package wrote. Without this the second run starts from an imported package and
#    proves nothing about a first run.
if ($Project) {
  $manifest = Join-Path $Project "Packages\manifest.json"
  if (Test-Path $manifest) {
    $m = Get-Content $manifest -Raw | ConvertFrom-Json
    $mosaicDeps = @($m.dependencies.PSObject.Properties.Name | Where-Object { $_ -like "com.mosaic.*" })
    $hasRegistry = $m.PSObject.Properties.Name -contains "scopedRegistries" -and
                   ($m.scopedRegistries | Where-Object { $_.name -eq "Mosaic" })
    if ($mosaicDeps.Count -or $hasRegistry) {
      Act "remove $($mosaicDeps.Count) Mosaic package(s) and the Mosaic registry from $manifest" {
        foreach ($d in $mosaicDeps) { $m.dependencies.PSObject.Properties.Remove($d) }
        if ($m.PSObject.Properties.Name -contains "scopedRegistries") {
          $kept = @($m.scopedRegistries | Where-Object { $_.name -ne "Mosaic" })
          if ($kept.Count) { $m.scopedRegistries = $kept }
          else { $m.PSObject.Properties.Remove("scopedRegistries") }
        }
        ($m | ConvertTo-Json -Depth 40) | Set-Content $manifest -Encoding UTF8
      }
    } else { Write-Host "  (no Mosaic packages in the manifest)" -ForegroundColor DarkGray }
  } else { Write-Host "  ($Project is not a Unity project)" -ForegroundColor Red }

  # Unity mirrors scoped registries into ProjectSettings, where they survive a
  # manifest edit and quietly reappear on the next open.
  $pms = Join-Path $Project "ProjectSettings\PackageManagerSettings.asset"
  if ((Test-Path $pms) -and ((Get-Content $pms -Raw) -match "Mosaic")) {
    Act "remove the Mosaic registry from $pms" {
      $text = Get-Content $pms -Raw
      $text = [regex]::Replace($text, "(?ms)^\s*- m_Id:.*?(?=^\s*- m_Id:|^\s*m_UserSelectedRegistryName|\z)", {
        param($m) if ($m.Value -match "Mosaic") { "" } else { $m.Value } })
      Set-Content $pms $text -Encoding UTF8
    }
  }

  $lock = Join-Path $Project "Packages\packages-lock.json"
  if (Test-Path $lock) { Act "delete $lock (Unity rebuilds it)" { Remove-Item -Force $lock } }

  Get-ChildItem (Join-Path $Project "Library\PackageCache") -Filter "com.mosaic.bridge*" -Directory -ErrorAction SilentlyContinue |
    ForEach-Object { $d = $_.FullName; Act "delete cached package $($_.Name)" { Remove-Item -Recurse -Force $d } }

  $mcp = Join-Path $Project ".mcp.json"
  if (Test-Path $mcp) { Act "delete $mcp" { Remove-Item -Force $mcp } }
} else {
  Write-Host "  (no -Project given: the Unity project was left untouched)" -ForegroundColor DarkGray
}

# 5. Unity registry access for this service, written once per machine by setup
$upm = Join-Path $env:USERPROFILE ".upmconfig.toml"
if (Test-Path $upm) {
  $text = Get-Content $upm -Raw
  if ($text -match '\[npmAuth\."[^"]*registry"\]') {
    Act "remove the Mosaic block from $upm" {
      $cleaned = [regex]::Replace($text, '(?s)\[npmAuth\."[^"]*registry"\].*?(?=\[|$)', '')
      Set-Content $upm $cleaned.Trim() -Encoding UTF8
    }
  } else { Write-Host "  (no Mosaic entry in .upmconfig.toml)" -ForegroundColor DarkGray }
}

# 6. The downloaded binary, so the next test starts from the download step
Get-ChildItem (Join-Path $env:USERPROFILE "Downloads") -Filter "mosaic-connector*" -ErrorAction SilentlyContinue |
  ForEach-Object { $f = $_.FullName; Act "delete $f" { Remove-Item -Force $f } }

Write-Host ""
if ($Apply) {
  Write-Host "Done. Close and reopen Unity so it re-resolves packages, then start again from the install page." -ForegroundColor Green
} else {
  Write-Host "Nothing was changed. Re-run with -Apply to remove the items listed above." -ForegroundColor Cyan
}
