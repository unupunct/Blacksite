; BLACKSITE – Tactical Interface Simulator – Windows installer (per-user, no administrator rights needed)
#define AppName "BLACKSITE"
#define AppVersion "1.0.0"
#ifndef SourceDir
  #define SourceDir "..\output\windows\selfcontained"
#endif
#ifndef OutDir
  #define OutDir "..\output\windows"
#endif

[Setup]
AppId={{6F1C9B1A-3B0E-4F4D-9D54-B1AC85170001}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=BLACKSITE
DefaultDirName={autopf}\BLACKSITE
DefaultGroupName=BLACKSITE
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutDir}
OutputBaseFilename=BLACKSITE-Setup
SetupIconFile=..\src\Blacksite.Desktop\Assets\blacksite.ico
UninstallDisplayIcon={app}\BLACKSITE.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\BLACKSITE"; Filename: "{app}\BLACKSITE.exe"
Name: "{autodesktop}\BLACKSITE"; Filename: "{app}\BLACKSITE.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BLACKSITE.exe"; Description: "Launch BLACKSITE"; Flags: nowait postinstall skipifsilent

[UninstallRegistry]
; Remove the optional per-user startup entry if the user enabled it inside the app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "BLACKSITE"; Flags: dontcreatekey

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'BLACKSITE');
end;
