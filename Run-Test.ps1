<#
.SYNOPSIS
    Launches LockGuard GUI application.
.DESCRIPTION
    Double-click or right-click -> Run with PowerShell to start LockGuard.
    For full adapter control, run as Administrator.
#>

$scriptPath = Join-Path $PSScriptRoot "LockGuard.ps1"
if (-not (Test-Path $scriptPath)) {
    Write-Host "[ERROR] LockGuard.ps1 not found at: $scriptPath" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

& $scriptPath
