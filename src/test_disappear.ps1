$ErrorActionPreference = 'Continue'
$out = 'C:\Users\Bogdan\Documents\Default Project\LockGuard\src\test_disappear.log'

$sb = New-Object System.Text.StringBuilder

$sb.AppendLine("=== START " + (Get-Date).ToString('HH:mm:ss') + " ===") | Out-Null

# 1. current process snapshot
$sb.AppendLine("--- before ---") | Out-Null
$pr = @(Get-Process LockGuard -ErrorAction SilentlyContinue)
if ($pr.Count -eq 0) { $sb.AppendLine("NO LockGuard process before") | Out-Null }
foreach ($p in $pr) { $sb.AppendLine(("  pid={0} session={1} start={2} resp={3}" -f $p.Id,$p.SessionId,$p.StartTime.ToString('HH:mm:ss'),$p.Responding)) | Out-Null }

# 2. find the active physical internet adapter (connected, enabled)
$sb.AppendLine("--- adapters ---") | Out-Null
$targets = @()
try {
  $nics = Get-NetAdapter -ErrorAction Stop | Where-Object { $_.Physical -and $_.Status -eq 'Up' }
  foreach ($n in $nics) { $sb.AppendLine(("  {0} | {1} | {2}" -f $n.Name, $n.InterfaceDescription, $n.Status)) | Out-Null; $targets += $n.Name }
} catch { $sb.AppendLine("  adapter enum err: " + $_.Exception.Message) | Out-Null }

# 3. disable them EXACTLY like LockGuard does (netsh admin=disable)
if ($targets.Count -gt 0) {
  $sb.AppendLine("--- disabling: " + ($targets -join ', ')) | Out-Null
  foreach ($n in $targets) {
    $r = netsh interface set interface name=$n admin=disable 2>&1 | Out-String
    $sb.AppendLine(("  [netsh disable " + $n + "] " + $r.Trim())) | Out-Null
  }
}
else { $sb.AppendLine("NO ADAPTER to disable - skip") | Out-Null }

# 4. wait and watch: does the LockGuard process die on its own when network is cut?
Start-Sleep -Seconds 12
$sb.AppendLine("--- after 12s with network OFF ---") | Out-Null
$pr2 = @(Get-Process LockGuard -ErrorAction SilentlyContinue)
if ($pr2.Count -eq 0) { $sb.AppendLine("!! LOCKGUARD IS GONE - process died on network cut !!") | Out-Null }
foreach ($p in $pr2) { $sb.AppendLine(("  pid={0} start={1} resp={2} windowHandle=0x{3:X}" -f $p.Id,$p.StartTime.ToString('HH:mm:ss'),$p.Responding,$p.MainWindowHandle)) | Out-Null }

# 5. RE-ENABLE immediately (restore internet - safe rollback)
$sb.AppendLine("--- re-enabling network NOW ---") | Out-Null
foreach ($n in $targets) {
  $r = netsh interface set interface name=$n admin=enable 2>&1 | Out-String
  $sb.AppendLine(("  [netsh enable " + $n + "] " + $r.Trim())) | Out-Null
}

$sb.AppendLine("=== DONE " + (Get-Date).ToString('HH:mm:ss') + " ===") | Out-Null
Set-Content -Path $out -Value $sb.ToString() -Encoding UTF8
Write-Host "test_disappear complete -> $out"