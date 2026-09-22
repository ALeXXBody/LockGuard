#!/usr/bin/env bash
# Build NetCurfew app + NSIS installer on Linux.
# Requires: mono-mcs, binutils-mingw-w64 (windres), nsis.
# Output:   src/NetCurfew.exe, NetCurfew-1.0-Setup.exe (repo root)
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="1.0"

echo "[1/3] Generating Win32 resources (icon + manifest + version info)..."
rm -f src/NetCurfew.res
cat > src/NetCurfew.rc <<EOF
1 ICON "netcurfew.ico"
1 24 "NetCurfew.manifest"
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
      VALUE "CompanyName",      "NetCurfew"
      VALUE "FileDescription",  "NetCurfew - password-protected night internet guard"
      VALUE "FileVersion",      "1.0.0.0"
      VALUE "InternalName",     "NetCurfew"
      VALUE "LegalCopyright",   "Copyright (C) 2026"
      VALUE "OriginalFilename", "NetCurfew.exe"
      VALUE "ProductName",      "NetCurfew"
      VALUE "ProductVersion",   "1.0.0.0"
    END
  END
  BLOCK "VarFileInfo"
  BEGIN
    VALUE "Translation", 0x409, 1200
  END
END
EOF
i686-w64-mingw32-windres src/NetCurfew.rc -O res -o src/NetCurfew.res
echo "      OK: src/NetCurfew.res"

echo "[2/3] Compiling NetCurfew.exe (mcs)..."
rm -f src/NetCurfew.exe
mcs -nologo -target:winexe -optimize+ -platform:anycpu \
    -out:src/NetCurfew.exe \
    -r:System.dll -r:System.Core.dll -r:System.Drawing.dll \
    -r:System.Windows.Forms.dll -r:System.Management.dll \
    -r:System.Web.Extensions.dll -r:System.Xml.dll \
    -win32res:src/NetCurfew.res \
    src/AssemblyInfo.cs src/NetCurfew.cs
echo "      OK: src/NetCurfew.exe"

echo "[3/3] Building NetCurfew-${VERSION}-Setup.exe (makensis)..."
(cd installer && makensis -V2 NetCurfew.nsi)
echo "      OK: NetCurfew-${VERSION}-Setup.exe"

echo "Done."
