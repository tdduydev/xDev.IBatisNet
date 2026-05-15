!ifndef APP_VERSION
  !define APP_VERSION "0.1.0"
!endif

!ifndef ARCH
  !define ARCH "win-x64"
!endif

!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR is required"
!endif

!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "xDev.IBatisNet.XmlDebugger-${APP_VERSION}-${ARCH}-Setup.exe"
!endif

Name "xDev iBATIS XML Debugger"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\xDev\IBatisNetXmlDebugger-${ARCH}"
RequestExecutionLevel user
SetCompressor /SOLID lzma

Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"
  CreateDirectory "$SMPROGRAMS\xDev"
  CreateShortcut "$SMPROGRAMS\xDev\xDev iBATIS XML Debugger (${ARCH}).lnk" "$INSTDIR\XDev.IBatisNet.XmlDebugger.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\xDev\xDev iBATIS XML Debugger (${ARCH}).lnk"
  RMDir "$SMPROGRAMS\xDev"
  RMDir /r "$INSTDIR"
SectionEnd
