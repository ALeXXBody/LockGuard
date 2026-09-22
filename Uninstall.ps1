<#
.SYNOPSIS
    Removes LockGuard and re-enables internet.
#>

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        "Uninstall.ps1 must be run as Administrator.`n`nRight-click Uninstall.ps1 and select 'Run as administrator'.",
        "LockGuard Uninstaller", "OK", "Warning"
    )
    exit 1
}

$TaskName = "LockGuard_NightInternetMonitor"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LockGuard Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Remove task
$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($task) {
    if ($task.State -eq 'Running') { Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue }
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "[OK] Removed scheduled task" -ForegroundColor Green
} else {
    Write-Host "[INFO] Task not found" -ForegroundColor Yellow
}

# Kill running LockGuard processes (use WMI for CommandLine)
$allPs = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue
$killed = 0
foreach ($proc in $allPs) {
    if ($proc.CommandLine -like '*LockGuard.ps1*') {
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        $killed++
    }
}
if ($killed -gt 0) { Write-Host "[OK] Stopped $killed LockGuard process(es)" -ForegroundColor Green }

# Re-enable adapters
$disabled = Get-NetAdapter -ErrorAction SilentlyContinue |
    Where-Object { $_.Status -eq 'Disabled' -and $_.InterfaceDescription -notlike '*Loopback*' }
if ($disabled) {
    foreach ($a in $disabled) {
        Enable-NetAdapter -Name $a.Name -Confirm:$false -ErrorAction SilentlyContinue
        Write-Host "[OK] Re-enabled: $($a.Name)" -ForegroundColor Green
    }
} else {
    Write-Host "[OK] No disabled adapters" -ForegroundColor Green
}

Write-Host ""
Write-Host "Uninstall complete! Logs preserved at C:\ProgramData\LockGuard\Logs" -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to close"
