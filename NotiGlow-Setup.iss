[Setup]
AppId={{8B84C3F0-7613-4B94-A3D9-9C9D7881B30E}
AppName=NotiGlow
AppVersion=1.1.0
AppPublisher=NotiGlow Project
AppPublisherURL=https://github.com/owergungor/NotiGlow
AppSupportURL=https://github.com/owergungor/NotiGlow/issues
AppUpdatesURL=https://github.com/owergungor/NotiGlow/releases
DefaultDirName={autopf}\NotiGlow
DefaultGroupName=NotiGlow
UninstallDisplayIcon={app}\NotiGlow.exe
OutputDir=release
OutputBaseFilename=NotiGlow-Setup-x64
SetupIconFile=Assets\NotiGlow.ico
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
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
