try {
    & "C:\Users\Bogdan\Documents\Default Project\LockGuard\LockGuard.ps1"
} catch {
    $errFile = "C:\ProgramData\LockGuard\Logs\error.log"
    $_.Exception | Out-File $errFile
    $_.ScriptStackTrace | Out-File $errFile -Append
    Write-Host "ERROR: $($_.Exception.Message)"
}
