#!/usr/bin/env bash
# Build LockGuard app + NSIS installer on Linux.
# Requires: mono-mcs, binutils-mingw-w64 (windres), nsis.
# Output:   src/LockGuard.exe, LockGuard-1.0-Setup.exe (repo root)
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="1.0"

echo "[1/3] Generating Win32 resources (icon + manifest + version info)..."
rm -f src/LockGuard.res
cat > src/LockGuard.rc <<EOF
1 ICON "lockguard.ico"
1 24 "LockGuard.manifest"
1 VERSIONINFO
FILEVERSION     1,0,0,0
PRODUCTVERSION  1,0,0,0
FILEOS          0x40004L
FILETYPE        0x1L
BEGIN
  BLOCK "StringFileInfo"
  BEGIN
    BLOCK "040904B0"
    BEGIN
      VALUE "CompanyName",      "LockGuard"
      VALUE "FileDescription",  "LockGuard - password-protected night internet guard"
      VALUE "FileVersion",      "1.0.0.0"
      VALUE "InternalName",     "LockGuard"
      VALUE "LegalCopyright",   "Copyright (C) 2026"
      VALUE "OriginalFilename", "LockGuard.exe"
      VALUE "ProductName",      "LockGuard"
      VALUE "ProductVersion",   "1.0.0.0"
    END
  END
  BLOCK "VarFileInfo"
  BEGIN
    VALUE "Translation", 0x409, 1200
  END
END
EOF
i686-w64-mingw32-windres src/LockGuard.rc -O res -o src/LockGuard.res
echo "      OK: src/LockGuard.res"

echo "[2/3] Compiling LockGuard.exe (mcs)..."
rm -f src/LockGuard.exe
mcs -nologo -target:winexe -optimize+ -platform:anycpu \
    -out:src/LockGuard.exe \
    -r:System.dll -r:System.Core.dll -r:System.Drawing.dll \
    -r:System.Windows.Forms.dll -r:System.Management.dll \
    -r:System.Web.Extensions.dll -r:System.Xml.dll \
    -win32res:src/LockGuard.res \
    src/AssemblyInfo.cs src/LockGuard.cs
echo "      OK: src/LockGuard.exe"

echo "[3/3] Building LockGuard-${VERSION}-Setup.exe (makensis)..."
(cd installer && makensis -V2 LockGuard.nsi)
echo "      OK: LockGuard-${VERSION}-Setup.exe"

echo "Done."
