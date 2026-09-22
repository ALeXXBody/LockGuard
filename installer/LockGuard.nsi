; =====================================================================
;  LockGuard 1.0 - NSIS installer
;  Builds LockGuard-1.0-Setup.exe: UAC-elevated Windows installer with
;  wizard UI, shortcuts, autostart scheduled task, audit policies and a
;  real uninstaller registered in Programs and Features.
;
;  Build:  makensis LockGuard.nsi   (run from this directory;
;          expects ../src/LockGuard.exe and ../src/lockguard.ico)
; =====================================================================

Unicode true
ManifestDPIAware true

!define APP_NAME        "LockGuard"
!define APP_VERSION     "1.0"
!define APP_VERSION_F   "1.0.0.0"
!define PUBLISHER       "LockGuard"
!define TASK_NAME       "LockGuard_NightInternetMonitor"
!define UNINST_KEY      "Software\Microsoft\Windows\CurrentVersion\Uninstall\LockGuard"
!define DATA_DIR_NAME   "LockGuard"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "..\LockGuard-${APP_VERSION}-Setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
RequestExecutionLevel admin
ShowInstDetails show
ShowUnInstDetails show
SetCompressor /SOLID lzma

VIProductVersion "${APP_VERSION_F}"
VIAddVersionKey /LANG=1033 "ProductName"     "${APP_NAME}"
VIAddVersionKey /LANG=1033 "FileDescription" "${APP_NAME} Installer"
VIAddVersionKey /LANG=1033 "FileVersion"     "${APP_VERSION_F}"
VIAddVersionKey /LANG=1033 "ProductVersion"  "${APP_VERSION_F}"
VIAddVersionKey /LANG=1033 "CompanyName"     "${PUBLISHER}"
VIAddVersionKey /LANG=1033 "LegalCopyright"  "(C) 2026 LockGuard"
VIAddVersionKey /LANG=1033 "OriginalFilename" "LockGuard-${APP_VERSION}-Setup.exe"

!define MUI_ABORTWARNING
!define MUI_ICON  "..\src\lockguard.ico"
!define MUI_UNICON "..\src\lockguard.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\LockGuard.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start LockGuard now"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${Else}
    SetRegView 32
    StrCpy $INSTDIR "$PROGRAMFILES32\${APP_NAME}"
  ${EndIf}
FunctionEnd

Function un.onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${Else}
    SetRegView 32
  ${EndIf}
FunctionEnd

Section "Install" SEC_INSTALL
  SectionIn RO
  SetOutPath "$INSTDIR"

  DetailPrint "Stopping any running instance of LockGuard..."
  nsExec::Exec 'taskkill /F /IM LockGuard.exe /T'
  Pop $0
  Sleep 400

  File "..\src\LockGuard.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  DetailPrint "Registering application (Programs and Features)..."
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\LockGuard.exe"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1

  DetailPrint "Creating shortcuts..."
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"           "$INSTDIR\LockGuard.exe"
  CreateShortCut  "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortCut  "$DESKTOP\${APP_NAME}.lnk"                          "$INSTDIR\LockGuard.exe"

  DetailPrint "Creating autostart scheduled task (${TASK_NAME})..."
  nsExec::ExecToLog 'schtasks /Create /F /RL HIGHEST /SC ONLOGON /TN "${TASK_NAME}" /TR "\"$INSTDIR\LockGuard.exe\""'
  Pop $0
  DetailPrint "  schtasks exit code: $0 (0 = OK; you can also toggle auto-start in LockGuard Settings)"

  DetailPrint "Enabling lock/unlock audit policies..."
  nsExec::ExecToLog 'auditpol /Set /Subcategory:"Logon" /Success:Enable /Failure:Enable'
  Pop $0
  nsExec::ExecToLog 'auditpol /Set /Subcategory:"Logoff" /Success:Enable /Failure:Enable'
  Pop $0
  nsExec::ExecToLog 'auditpol /Set /Subcategory:"Special Logon" /Success:Enable /Failure:Enable'
  Pop $0
  DetailPrint "  auditpol exit code: $0 (0 = OK)"
SectionEnd

Section "Uninstall"
  DetailPrint "Stopping LockGuard..."
  nsExec::Exec 'taskkill /F /IM LockGuard.exe /T'
  Pop $0
  Sleep 400

  DetailPrint "Removing autostart scheduled task..."
  nsExec::Exec 'schtasks /Delete /F /TN "${TASK_NAME}"'
  Pop $0

  DetailPrint "Removing shortcuts..."
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  DetailPrint "Removing application files..."
  Delete "$INSTDIR\LockGuard.exe"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir  "$INSTDIR"

  DetailPrint "Removing registration..."
  DeleteRegKey HKLM "${UNINST_KEY}"

  MessageBox MB_YESNO|MB_ICONQUESTION \
    "Also delete LockGuard data (password, settings, logs)?$\n$\nChoose 'No' to keep your configuration for a future reinstall." \
    /SD IDNO IDNO skip_data
  ExpandEnvStrings $0 "%ProgramData%"
  DetailPrint "Removing data: $0\${DATA_DIR_NAME}"
  RMDir /r "$0\${DATA_DIR_NAME}"
skip_data:
  DetailPrint "Uninstall complete."
SectionEnd
