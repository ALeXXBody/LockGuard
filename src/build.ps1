# Build LockGuard v1.0 - compiles LockGuard.exe, then the NSIS installer if available
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$ver = "1.0"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }

# ---- App manifest (asInvoker + dpiAware) ----
$appManifest = Join-Path $PSScriptRoot "LockGuard.manifest"
@'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="LockGuard.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
    </windowsSettings>
  </application>
</assembly>
'@ | Set-Content -Path $appManifest -Encoding UTF8
Write-Host "[OK] Manifest written" -ForegroundColor Green

# ---- App icon ----
$icoPath = Join-Path $PSScriptRoot "lockguard.ico"
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(64, 64)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$shield = New-Object System.Drawing.Drawing2D.GraphicsPath
$shield.AddPolygon([System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point(32, 4)),
    (New-Object System.Drawing.Point(60, 10)),
    (New-Object System.Drawing.Point(60, 30)),
    (New-Object System.Drawing.Point(32, 60)),
    (New-Object System.Drawing.Point(4, 30)),
    (New-Object System.Drawing.Point(4, 10))
))
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Rectangle(0, 0, 64, 64)),
    [System.Drawing.Color]::FromArgb(255, 0, 217, 255),
    [System.Drawing.Color]::FromArgb(255, 162, 120, 255),
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
)
$g.FillPath($brush, $shield)

$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.FillEllipse($white, 22, 18, 20, 20)
$g.FillRectangle($white, 29, 34, 6, 12)
$g.Dispose()
$iconHandle = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$fs = [System.IO.File]::Create($icoPath)
$icon.Save($fs)
$fs.Close()
$g.Dispose(); $bmp.Dispose(); $icon.Dispose()
Write-Host "[OK] Icon: $icoPath" -ForegroundColor Green

# ---- Compile LockGuard.exe ----
$appOut = Join-Path $PSScriptRoot "LockGuard.exe"
Write-Host "`nCompiling LockGuard.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /out:$appOut `
    "/reference:System.dll" `
    "/reference:System.Core.dll" `
    "/reference:System.Drawing.dll" `
    "/reference:System.Windows.Forms.dll" `
    "/reference:System.Management.dll" `
    "/reference:System.Web.Extensions.dll" `
    "/reference:System.Xml.dll" `
    "/win32icon:$icoPath" `
    "/win32manifest:$appManifest" `
    (Join-Path $PSScriptRoot "AssemblyInfo.cs") `
    (Join-Path $PSScriptRoot "LockGuard.cs")
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: compile failed (code $LASTEXITCODE)" -ForegroundColor Red; exit 1 }
Write-Host "[OK] App: $appOut ($([math]::Round((Get-Item $appOut).Length/1KB)) KB)" -ForegroundColor Green

# ---- NSIS installer (optional, if makensis is on PATH) ----
$nsis = Get-Command makensis -ErrorAction SilentlyContinue
if ($nsis) {
    Write-Host "`nBuilding installer..." -ForegroundColor Cyan
    Push-Location (Join-Path $PSScriptRoot "..\installer")
    & makensis -V2 LockGuard.nsi
    Pop-Location
    if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: installer build failed (code $LASTEXITCODE)" -ForegroundColor Red; exit 1 }
    Write-Host "[OK] Installer built" -ForegroundColor Green
} else {
    Write-Host "`n[SKIP] makensis not found - installer not built." -ForegroundColor Yellow
    Write-Host "       Install NSIS (https://nsis.sourceforge.io) and re-run to build the setup exe." -ForegroundColor Yellow
}

Write-Host "`nBuild complete!" -ForegroundColor Green
