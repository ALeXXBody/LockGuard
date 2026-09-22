<#
.SYNOPSIS
    Installs LockGuard to run at startup and enables audit policies.
.DESCRIPTION
    Creates a scheduled task and configures Windows audit policies.
    Must be run as Administrator. If UAC prompt appears, click Yes.
#>

# Check admin rights first - show visible error if not admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        "Install.ps1 must be run as Administrator.`n`nRight-click Install.ps1 and select 'Run as administrator'.",
        "LockGuard Installer", "OK", "Warning"
    )
    exit 1
}

$TaskName   = "LockGuard_NightInternetMonitor"
$ScriptPath = Join-Path $PSScriptRoot "LockGuard.ps1"
$logDir     = "C:\ProgramData\LockGuard\Logs"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LockGuard Installer v2.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verify
if (-not (Test-Path $ScriptPath)) {
    Write-Host "[ERROR] LockGuard.ps1 not found!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Create log dir
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
Write-Host "[OK] Log directory: $logDir" -ForegroundColor Green

# Remove old task
$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existing) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "[OK] Removed old task" -ForegroundColor Yellow
}

# Create task - starts at login, runs as current user (not SYSTEM) for GUI access
# Use wscript + VBS wrapper to launch PowerShell truly hidden on Windows 11
$vbsPath = Join-Path $PSScriptRoot "Start LockGuard.vbs"
$action = New-ScheduledTaskAction `
    -Execute "wscript.exe" `
    -Argument "`"$vbsPath`"" `
    -WorkingDirectory $PSScriptRoot

$trigger = New-ScheduledTaskTrigger -AtLogon

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Days 365)

# Run as current user with normal privileges for GUI
$principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType Interactive `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "LockGuard: Night internet guard & intrusion monitor" `
    -Force | Out-Null

Write-Host "[OK] Scheduled task created (runs at login)" -ForegroundColor Green

# Enable audit policies
Write-Host ""
Write-Host "Configuring audit policies..." -ForegroundColor Cyan
auditpol /set /subcategory:"Logon" /success:enable /failure:enable 2>&1 | Out-Null
auditpol /set /subcategory:"Logoff" /success:enable /failure:enable 2>&1 | Out-Null
auditpol /set /subcategory:"Special Logon" /success:enable /failure:enable 2>&1 | Out-Null

# Verify
$check = auditpol /get /subcategory:"Logon" 2>&1
if ($check -match 'Success and Failure') {
    Write-Host "[OK] Audit policy: Logon = Success + Failure" -ForegroundColor Green
} elseif ($check -match 'Success') {
    Write-Host "[WARN] Audit policy: Logon = Success only" -ForegroundColor Yellow
} else {
    Write-Host "[WARN] Could not verify audit policy" -ForegroundColor Yellow
}

# Start now?
Write-Host ""
$startNow = Read-Host "Start LockGuard now? (Y/n)"
if ($startNow -ne 'n' -and $startNow -ne 'N') {
    Start-Process wscript.exe -ArgumentList "`"$vbsPath`""
    Write-Host "[OK] LockGuard started!" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Done! LockGuard will start on login." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Manual launch: Right-click Run-Test.ps1 -> Run with PowerShell" -ForegroundColor White
Write-Host "Logs:          $logDir" -ForegroundColor White
Write-Host "Uninstall:     .\Uninstall.ps1" -ForegroundColor Yellow
Write-Host ""
Read-Host "Press Enter to close"
