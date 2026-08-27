<#
.SYNOPSIS
  One-time offline bake of real OpenStreetMap tiles for the Alipiri Mettu -> Tirumala
  corridor, written straight into Assets/StreamingAssets/Tiles so TileBasemap.cs can load
  them on-device with no internet access at runtime.

.WHY THIS HAS TO RUN ON YOUR OWN MACHINE
  The same fetch attempted from the shared cloud sandbox got blocked outright by OSM's
  tile server (it started returning a "403 Access blocked - not following the tile usage
  policy" *image* with an HTTP 200 status, so it looked like success and got saved as if
  it were 1,361 real tiles). That's almost certainly the shared/datacenter IP looking like
  bulk-scraper traffic to their abuse detection, not the request pattern itself: this
  script fetches at well under OSM's documented 2-requests/second ceiling, sends a
  descriptive User-Agent (required by their usage policy), and is a one-time ~1,360-tile
  fetch for a ~2 km2 area - exactly the kind of individual/development use their policy is
  written to tolerate. Running it from your own residential connection should not hit the
  same block.

.USAGE
  Right-click this file -> Run with PowerShell, or from a terminal:
    powershell -ExecutionPolicy Bypass -File "Tools\FetchMapTiles.ps1"
  Takes roughly 15-20 minutes (rate-limited on purpose). Safe to re-run if interrupted -
  it skips tiles it already has.

.AFTER IT FINISHES
  Check the summary it prints. If any tile came back as exactly 6,987 bytes, that's OSM's
  block-notice image again (same one seen from the sandbox) - stop and let me know rather
  than shipping it. Otherwise just rebuild the APK; TileBasemap.cs already reads from this
  exact folder layout.
#>

$ErrorActionPreference = "Stop"

# --- AOI: covers the full Alipiri Mettu -> Tirumala corridor (matches the KML/PDF the
# route is drawn from), plus a small margin. Same bounds used for the earlier attempt.
$LatMin = 13.6440
$LatMax = 13.6740
$LonMin = 79.3505
$LonMax = 79.4080
$MinZoom = 14
$MaxZoom = 18

# 6,987 bytes is the exact size of OSM's "Access blocked" placeholder image seen earlier -
# any tile that comes back this size is that notice, not real map data.
$BlockedSizeBytes = 6987

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$OutDir = Join-Path $ProjectRoot "Assets\StreamingAssets\Tiles"
$UserAgent = "AlipiriAR-Navigation-OfflineTileBake/1.0 (one-time build-step download for an offline pilgrim-navigation app; contact: bhargav.m23@iiits.in)"

function Deg2Tile($latDeg, $lonDeg, $zoom) {
    $latRad = [Math]::PI * $latDeg / 180.0
    $n = [Math]::Pow(2, $zoom)
    $x = [Math]::Floor(($lonDeg + 180.0) / 360.0 * $n)
    $y = [Math]::Floor((1.0 - [Math]::Log([Math]::Tan($latRad) + 1.0 / [Math]::Cos($latRad)) / [Math]::PI) / 2.0 * $n)
    return @([int]$x, [int]$y)
}

$jobs = New-Object System.Collections.Generic.List[object]
for ($z = $MinZoom; $z -le $MaxZoom; $z++) {
    $tl = Deg2Tile $LatMax $LonMin $z
    $br = Deg2Tile $LatMin $LonMax $z
    $xlo = [Math]::Min($tl[0], $br[0]); $xhi = [Math]::Max($tl[0], $br[0])
    $ylo = [Math]::Min($tl[1], $br[1]); $yhi = [Math]::Max($tl[1], $br[1])
    for ($x = $xlo; $x -le $xhi; $x++) {
        for ($y = $ylo; $y -le $yhi; $y++) {
            $jobs.Add(@{ z = $z; x = $x; y = $y })
        }
    }
}

$total = $jobs.Count
Write-Host "Total tiles to fetch: $total"
Write-Host "Writing into: $OutDir"
Write-Host ""

$ok = 0
$skipped = 0
$blocked = New-Object System.Collections.Generic.List[string]
$failed = New-Object System.Collections.Generic.List[string]

for ($i = 0; $i -lt $total; $i++) {
    $job = $jobs[$i]
    $destDir = Join-Path $OutDir "$($job.z)\$($job.x)"
    $dest = Join-Path $destDir "$($job.y).png"

    if ((Test-Path $dest) -and ((Get-Item $dest).Length -gt 0) -and ((Get-Item $dest).Length -ne $BlockedSizeBytes)) {
        $skipped++
        $ok++
    }
    else {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        $url = "https://tile.openstreetmap.org/$($job.z)/$($job.x)/$($job.y).png"
        $attempt = 0
        $success = $false
        while ($attempt -lt 3 -and -not $success) {
            $attempt++
            try {
                Invoke-WebRequest -Uri $url -Headers @{ "User-Agent" = $UserAgent } -OutFile $dest -TimeoutSec 15 | Out-Null
                $success = $true
            }
            catch {
                if ($attempt -ge 3) {
                    $failed.Add("$($job.z)/$($job.x)/$($job.y): $($_.Exception.Message)")
                }
                else {
                    Start-Sleep -Milliseconds 1500
                }
            }
        }

        if ($success) {
            $size = (Get-Item $dest).Length
            if ($size -eq $BlockedSizeBytes) {
                $blocked.Add("$($job.z)/$($job.x)/$($job.y)")
            }
            else {
                $ok++
            }
        }

        # Stay well under OSM's tile usage policy rate ceiling (2 req/sec).
        Start-Sleep -Milliseconds 550
    }

    if ((($i + 1) % 50) -eq 0 -or ($i + 1) -eq $total) {
        Write-Host "progress $($i+1)/$total  ok=$ok  skipped=$skipped  blocked=$($blocked.Count)  failed=$($failed.Count)"
    }

    if ($blocked.Count -ge 10) {
        Write-Host ""
        Write-Host "STOPPING EARLY: 10+ tiles came back as OSM's block-notice image." -ForegroundColor Red
        Write-Host "Your connection is being blocked the same way the sandbox was. Don't keep retrying -" -ForegroundColor Red
        Write-Host "let me know and we'll pick a different data source (see the alternatives I listed)." -ForegroundColor Red
        break
    }
}

Write-Host ""
Write-Host "DONE  ok=$ok  skipped=$skipped  blocked=$($blocked.Count)  failed=$($failed.Count)"

if ($blocked.Count -gt 0) {
    Write-Host ""
    Write-Host "$($blocked.Count) tile(s) came back as OSM's 'Access blocked' notice image, not real map data:" -ForegroundColor Yellow
    $blocked | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
    Write-Host "Do not ship these - re-run later, or tell me and we'll switch approach."
}
if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "$($failed.Count) tile(s) failed after 3 attempts (network errors, not blocks):" -ForegroundColor Yellow
    $failed | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
}
if ($blocked.Count -eq 0 -and $failed.Count -eq 0) {
    Write-Host ""
    Write-Host "All tiles downloaded cleanly. Rebuild the APK in Unity to see the real map." -ForegroundColor Green
}
