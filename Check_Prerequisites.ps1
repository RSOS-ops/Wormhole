# Check_Prerequisites.ps1
# Logic to be used by the installer

function Check-Software {
    param (
        [string]$ProcessName,
        [string]$ServiceName,
        [string]$DownloadUrl,
        [string]$FriendlyName
    )

    Write-Host "Checking for $FriendlyName..." -NoNewline
    
    # Check 1: Is the service registered?
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if ($service) {
        Write-Host " FOUND." -ForegroundColor Green
        return $true
    }
    else {
        Write-Host " MISSING." -ForegroundColor Red
        $choice = Read-Host " > $FriendlyName is required. Open download page? (Y/N)"
        if ($choice -eq 'Y' -or $choice -eq 'y') {
            Start-Process $DownloadUrl
        }
        return $false
    }
}

Write-Host "=== WORMHOLE INSTALLER PRE-FLIGHT CHECK ===" -ForegroundColor Cyan
Write-Host ""

$missing = $false

# CHECK TAILSCALE
if (-not (Check-Software -ServiceName "Tailscale" -FriendlyName "Tailscale VPN" -DownloadUrl "https://tailscale.com/download")) {
    $missing = $true
}

# CHECK SUNSHINE
if (-not (Check-Software -ServiceName "SunshineService" -FriendlyName "Sunshine Streamer" -DownloadUrl "https://github.com/LizardByte/Sunshine/releases/latest")) {
    $missing = $true
}

Write-Host ""
if ($missing) {
    Write-Host "WARNING: Some required software is missing." -ForegroundColor Yellow
    Write-Host "Please install them and run this installer again." -ForegroundColor Yellow
} else {
    Write-Host "All systems go! Proceeding with installation..." -ForegroundColor Green
    # Here is where the actual file copying would happen
}

# --- KEEPS WINDOW OPEN ---
Write-Host ""
Read-Host "Press Enter to exit..."