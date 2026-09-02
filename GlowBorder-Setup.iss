[Setup]
AppName=Glow Border
AppVersion=2.0.0
AppPublisher=Glow Border Project
DefaultDirName={autopf}\Glow Border
DefaultGroupName=Glow Border
UninstallDisplayIcon={app}\GlowBorder.exe
OutputBaseFilename=GlowBorder-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start Glow Border automatically on Windows login"; GroupDescription: "System Startup:"

[Files]
Source: "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Glow Border"; Filename: "{app}\GlowBorder.exe"
Name: "{group}\{cm:UninstallProgram,Glow Border}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Glow Border"; Filename: "{app}\GlowBorder.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GlowBorder"; ValueData: """{app}\GlowBorder.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\GlowBorder.exe"; Description: "{cm:LaunchProgram,Glow Border}"; Flags: nowait postinstall skipifsilent
