; Reevun installer additions (electron-builder "nsis.include").
; A page after the folder choice lets the user pick the shortcuts; they are
; created here instead of by electron-builder, and left alone on silent
; auto-updates so a declined shortcut never comes back.

!include nsDialogs.nsh
!include LogicLib.nsh

!ifndef BUILD_UNINSTALLER

Var ShortcutsTitle
Var ShortcutsHint
Var DesktopLabel
Var StartMenuLabel
Var DesktopBox
Var StartMenuBox
Var WantDesktop
Var WantStartMenu

!macro reevunTexts
  ${If} $LANGUAGE == 1049
    StrCpy $ShortcutsTitle "Ярлыки"
    StrCpy $ShortcutsHint "Где создать ярлыки Reevun?"
    StrCpy $DesktopLabel "На рабочем столе"
    StrCpy $StartMenuLabel "В меню «Пуск»"
  ${ElseIf} $LANGUAGE == 1031
    StrCpy $ShortcutsTitle "Verknüpfungen"
    StrCpy $ShortcutsHint "Wo sollen Verknüpfungen zu Reevun erstellt werden?"
    StrCpy $DesktopLabel "Auf dem Desktop"
    StrCpy $StartMenuLabel "Im Startmenü"
  ${ElseIf} $LANGUAGE == 1034
    StrCpy $ShortcutsTitle "Accesos directos"
    StrCpy $ShortcutsHint "¿Dónde crear accesos directos a Reevun?"
    StrCpy $DesktopLabel "En el escritorio"
    StrCpy $StartMenuLabel "En el menú Inicio"
  ${ElseIf} $LANGUAGE == 1055
    StrCpy $ShortcutsTitle "Kısayollar"
    StrCpy $ShortcutsHint "Reevun kısayolları nerede oluşturulsun?"
    StrCpy $DesktopLabel "Masaüstünde"
    StrCpy $StartMenuLabel "Başlat menüsünde"
  ${ElseIf} $LANGUAGE == 2052
    StrCpy $ShortcutsTitle "快捷方式"
    StrCpy $ShortcutsHint "要在哪里创建 Reevun 快捷方式？"
    StrCpy $DesktopLabel "桌面"
    StrCpy $StartMenuLabel "开始菜单"
  ${Else}
    StrCpy $ShortcutsTitle "Shortcuts"
    StrCpy $ShortcutsHint "Where should Reevun shortcuts be created?"
    StrCpy $DesktopLabel "On the desktop"
    StrCpy $StartMenuLabel "In the Start menu"
  ${EndIf}
!macroend

!endif

!macro customPageAfterChangeDir
  Page custom ShortcutsPageShow ShortcutsPageLeave
!macroend

!ifndef BUILD_UNINSTALLER
Function ShortcutsPageShow
  !insertmacro reevunTexts
  !insertmacro MUI_HEADER_TEXT "$ShortcutsTitle" "$ShortcutsHint"
  nsDialogs::Create 1018
  Pop $0
  ${NSD_CreateCheckbox} 0 0 100% 12u "$DesktopLabel"
  Pop $DesktopBox
  ${NSD_CreateCheckbox} 0 18u 100% 12u "$StartMenuLabel"
  Pop $StartMenuBox
  ${If} $WantDesktop != "0"
    ${NSD_Check} $DesktopBox
  ${EndIf}
  ${If} $WantStartMenu != "0"
    ${NSD_Check} $StartMenuBox
  ${EndIf}
  nsDialogs::Show
FunctionEnd

Function ShortcutsPageLeave
  ${NSD_GetState} $DesktopBox $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $WantDesktop "1"
  ${Else}
    StrCpy $WantDesktop "0"
  ${EndIf}
  ${NSD_GetState} $StartMenuBox $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $WantStartMenu "1"
  ${Else}
    StrCpy $WantStartMenu "0"
  ${EndIf}
FunctionEnd
!endif

!macro customInstall
  ${IfNot} ${isUpdated}
    ${If} $WantDesktop != "0"
      CreateShortCut "$DESKTOP\${SHORTCUT_NAME}.lnk" "$INSTDIR\${APP_EXECUTABLE_FILENAME}"
      WinShell::SetLnkAUMI "$DESKTOP\${SHORTCUT_NAME}.lnk" "${APP_ID}"
    ${EndIf}
    ${If} $WantStartMenu != "0"
      CreateShortCut "$SMPROGRAMS\${SHORTCUT_NAME}.lnk" "$INSTDIR\${APP_EXECUTABLE_FILENAME}"
      WinShell::SetLnkAUMI "$SMPROGRAMS\${SHORTCUT_NAME}.lnk" "${APP_ID}"
    ${EndIf}
  ${EndIf}
!macroend

!macro customUnInstall
  ${IfNot} ${isUpdated}
    Delete "$DESKTOP\${SHORTCUT_NAME}.lnk"
    Delete "$SMPROGRAMS\${SHORTCUT_NAME}.lnk"
  ${EndIf}
!macroend
