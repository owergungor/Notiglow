[Setup]
AppName=NotiGlow
AppVersion=1.0.0
AppPublisher=NotiGlow Project
DefaultDirName={autopf}\NotiGlow
DefaultGroupName=NotiGlow
UninstallDisplayIcon={app}\NotiGlow.exe
OutputBaseFilename=NotiGlow-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start NotiGlow automatically on Windows login"; GroupDescription: "System Startup:"

[Files]
Source: "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\NotiGlow"; Filename: "{app}\NotiGlow.exe"
Name: "{group}\{cm:UninstallProgram,NotiGlow}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NotiGlow"; Filename: "{app}\NotiGlow.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NotiGlow"; ValueData: """{app}\NotiGlow.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\NotiGlow.exe"; Description: "{cm:LaunchProgram,NotiGlow}"; Flags: nowait postinstall skipifsilent
