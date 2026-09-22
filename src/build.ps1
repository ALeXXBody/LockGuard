# Build LockGuard v3.1 - compiles C# to two exes
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }

$refs = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Management.dll",
    "/reference:System.Web.Extensions.dll"
)

# Generate app icon
$icoPath = Join-Path $PSScriptRoot "lockguard.ico"
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(64, 64)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Shield body
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
    [System.Drawing.Color]::FromArgb(255, 68, 130, 245),
    [System.Drawing.Color]::FromArgb(255, 40, 85, 180),
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
)
$g.FillPath($brush, $shield)

# Keyhole
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

# Main app manifest (asInvoker + dpiAware)
$appManifest = Join-Path $PSScriptRoot "LockGuard.manifest"
@'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="3.1.0.0" name="LockGuard.app"/>
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

# Setup manifest (requireAdministrator)
$setupManifest = Join-Path $PSScriptRoot "LockGuardSetup.manifest"
@'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="3.1.0.0" name="LockGuardSetup.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
    </windowsSettings>
  </application>
</assembly>
'@ | Set-Content -Path $setupManifest -Encoding UTF8

Write-Host "[OK] Manifests written" -ForegroundColor Green

$appOut = Join-Path $PSScriptRoot "LockGuard.exe"
$setupOut = Join-Path $PSScriptRoot "LockGuardSetup.exe"

Write-Host "`nCompiling LockGuard.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /out:$appOut $refs "/win32icon:$icoPath" "/win32manifest:$appManifest" (Join-Path $PSScriptRoot "LockGuard.cs")
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: main app compile failed (code $LASTEXITCODE)" -ForegroundColor Red; exit 1 }

Write-Host "`nCompiling LockGuardSetup.exe..." -ForegroundColor Cyan
$setupRefs = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll"
)
& $csc /nologo /target:winexe /optimize+ /out:$setupOut $setupRefs "/win32icon:$icoPath" "/win32manifest:$setupManifest" "/resource:$appOut,LockGuard.exe" (Join-Path $PSScriptRoot "LockGuardSetup.cs")
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: setup compile failed (code $LASTEXITCODE)" -ForegroundColor Red; exit 1 }

Write-Host "`nBuild complete!" -ForegroundColor Green
Write-Host "  App:     $appOut ($([math]::Round((Get-Item $appOut).Length/1KB)) KB)"
Write-Host "  Setup:   $setupOut ($([math]::Round((Get-Item $setupOut).Length/1KB)) KB)"