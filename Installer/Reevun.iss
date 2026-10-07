; Reevun_Setup.exe (Inno Setup): asks for the folder and the shortcuts, in
; the website's six languages, and starts the app at the end. The app's own
; updates run it silently (Updates.cs); /relaunch=1 then starts the new
; version, and the shortcuts chosen at first stay as they were.
; Built by the release workflow: iscc /DVersion=1.0.<run number> Reevun.iss

#ifndef Version
  #define Version "1.0.0"
#endif

[Setup]
AppId={{6C1E1A0B-5B1D-4C55-9B0E-2F4E6B8A9D31}
AppName=Reevun
AppVersion={#Version}
AppVerName=Reevun
AppPublisher=Reevun Software LLC
AppPublisherURL=https://reevun.app
AppCopyright=© Reevun Software LLC
VersionInfoVersion={#Version}
; For the person installing, no administrator needed; the folder can be
; changed.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Reevun
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=yes
WizardStyle=modern
WizardImageFile=sidebar.bmp
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\Reevun.exe
UninstallDisplayName=Reevun
ShowLanguageDialog=auto
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; An open Reevun is closed for the install (and the update) and not
; restarted by the installer itself.
CloseApplications=force
RestartApplications=no
Compression=lzma2/max
SolidCompression=yes
OutputDir=..\release
OutputBaseFilename=Reevun_Setup

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "zh"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
ru.Shortcuts=Ярлыки
en.Shortcuts=Shortcuts
de.Shortcuts=Verknüpfungen
es.Shortcuts=Accesos directos
tr.Shortcuts=Kısayollar
zh.Shortcuts=快捷方式
ru.Desktop=На рабочем столе
en.Desktop=On the desktop
de.Desktop=Auf dem Desktop
es.Desktop=En el escritorio
tr.Desktop=Masaüstünde
zh.Desktop=桌面
ru.StartMenu=В меню «Пуск»
en.StartMenu=In the Start menu
de.StartMenu=Im Startmenü
es.StartMenu=En el menú Inicio
tr.StartMenu=Başlat menüsünde
zh.StartMenu=开始菜单

[Tasks]
Name: "desktop"; Description: "{cm:Desktop}"; GroupDescription: "{cm:Shortcuts}"
Name: "startmenu"; Description: "{cm:StartMenu}"; GroupDescription: "{cm:Shortcuts}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Reevun"; Filename: "{app}\Reevun.exe"; Tasks: desktop
Name: "{autoprograms}\Reevun"; Filename: "{app}\Reevun.exe"; Tasks: startmenu

[Run]
Filename: "{app}\Reevun.exe"; Description: "{cm:LaunchProgram,Reevun}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Reevun.exe"; Flags: nowait; Check: Relaunch

[Code]
// After the app's own silent update: start the new version.
function Relaunch: Boolean;
begin
  Result := WizardSilent and (ExpandConstant('{param:relaunch|0}') = '1');
end;
