; kofrenot installer (Inno Setup 6.3 or later).
; Built by build\installer.cmd, which publishes the app and passes the version:
;   ISCC.exe /DAppVersion=<version> /DPublishDir=<published app folder> build\installer\kofrenot.iss
;
; Per-user install: no administrator rights, installed in %LOCALAPPDATA%\Programs\kofrenot.
; User data (%LOCALAPPDATA%\kofrenot: settings, logs) is left in place on uninstall.

#ifndef AppVersion
  #error AppVersion is not defined. Run build\installer.cmd instead of compiling this file directly.
#endif
#ifndef PublishDir
  #error PublishDir is not defined. Run build\installer.cmd instead of compiling this file directly.
#endif

#define AppName "kofrenot"
#define AppExe "kofrenot.exe"

[Setup]
; Fixed forever: it is how Windows recognizes an upgrade of the same application.
AppId={{15204DF4-E0F7-4459-B9B8-2F79C1CA0AB5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
; Also the setup's CompanyName; matches <Company> in Directory.Build.props.
AppPublisher=dlabs
VersionInfoVersion={#AppVersion}
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; The app creates this mutex (SingleInstance.cs): setup and uninstall ask to quit kofrenot first.
AppMutex=Local\kofrenot-single-instance
SetupIconFile=..\..\src\kofrenot.App\Assets\kofrenot.ico
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\..\artifacts\installer
OutputBaseFilename=kofrenot-setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start kofrenot now"; Flags: nowait postinstall skipifsilent
Filename: "{win}\notepad.exe"; Parameters: """{app}\CHANGELOG.md"""; Description: "Open the changelog";   Flags: nowait postinstall skipifsilent unchecked
