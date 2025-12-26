# AUTO_INSTALL_DEPENDENCIES.ps1
# This script forcefully attempts to install and configure missing services.

function Install-Component {
    param (
        [string]$Name,
        [string]$WingetId,
        [string]$WebUrl
    )

    Write-Host "--- INSTALLING: $Name ---" -ForegroundColor Cyan
    
    # 1. Try Winget (Silent Install)
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-Host " > Winget detected. Attempting silent install..." -ForegroundColor Gray
        try {
            # --accept-source-agreements is crucial for first-time runs
            winget install --id $WingetId -e --silent --accept-package-agreements --accept-source-agreements
            Write-Host " > Winget command completed." -ForegroundColor Green
            return $true
        }
        catch {
            Write-Host " ! Winget install failed." -ForegroundColor Red
        }
    }

    # 2. Fallback (Manual Browser Download)
    Write-Host " > Opening official download page..." -ForegroundColor Yellow
    Start-Process $WebUrl
    
    # Pause to let user install manually
    Read-Host " > Press Enter once you have finished installing $Name..."
    return $true
}

function Configure-Tailscale {
    Write-Host "--- CONFIGURING TAILSCALE ---" -ForegroundColor Cyan
    if (Get-Command tailscale -ErrorAction SilentlyContinue) {
        Write-Host " > Launching Login Window..." -ForegroundColor Gray
        Start-Process "tailscale" -ArgumentList "up" -NoNewWindow
    } else {
        Write-Host " ! Tailscale command not found. Did install finish?" -ForegroundColor Red
    }
}

function Configure-Sunshine {
    Write-Host "--- CONFIGURING SUNSHINE ---" -ForegroundColor Cyan
    Write-Host " > Opening Web UI to create credentials..." -ForegroundColor Gray
    Start-Process "https://localhost:47990"
}

# === MAIN LOGIC ===

Write-Host "=== WORMHOLE DEPENDENCY INSTALLER ===" -ForegroundColor Magenta
Write-Host "This script will download and configure required services."
Write-Host ""

# 1. Install Tailscale if missing
if (-not (Get-Service "Tailscale" -ErrorAction SilentlyContinue)) {
    Install-Component -Name "Tailscale" -WingetId "Tailscale.Tailscale" -WebUrl "https://tailscale.com/download"
    # Wait for service to register
    Start-Sleep -Seconds 5
} else {
    Write-Host " > Tailscale is already installed." -ForegroundColor Gray
}

# 2. Configure Tailscale (Always run 'up' just in case they aren't logged in)
Configure-Tailscale

# 3. Install Sunshine if missing
if (-not (Get-Service "SunshineService" -ErrorAction SilentlyContinue)) {
    Install-Component -Name "Sunshine" -WingetId "LizardByte.Sunshine" -WebUrl "https://github.com/LizardByte/Sunshine/releases/latest"
    # Wait for service to register
    Start-Sleep -Seconds 5
} else {
    Write-Host " > Sunshine is already installed." -ForegroundColor Gray
}

# 4. Configure Sunshine
Configure-Sunshine

Write-Host ""
Write-Host "=== SETUP COMPLETE ===" -ForegroundColor Green
Write-Host "You can now run the Wormhole Console."
Read-Host "Press Enter to exit..."