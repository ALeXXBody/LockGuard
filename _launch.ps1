$ErrorActionPreference = "Stop"
$errFile = "C:\ProgramData\LockGuard\Logs\crash.log"
try {
    & "C:\Users\Bogdan\Documents\Default Project\LockGuard\LockGuard.ps1"
} catch {
    "CRASH: $($_.Exception.Message)" | Out-File $errFile
    "STACK: $($_.ScriptStackTrace)" | Out-File $errFile -Append
    "TIME: $(Get-Date)" | Out-File $errFile -Append
}
