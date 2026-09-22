#!/usr/bin/env bash
# Build LockGuard app + NSIS installer on Linux.
# Requires: mono-mcs, binutils-mingw-w64 (windres), nsis.
# Output:   src/LockGuard.exe, LockGuard-3.1-Setup.exe (repo root)
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="3.1"

echo "[1/3] Generating Win32 resources (icon + manifest) via windres..."
rm -f src/LockGuard.res
cat > src/LockGuard.rc <<EOF
1 ICON "lockguard.ico"
1 24 "LockGuard.manifest"
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
