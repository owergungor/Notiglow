[Setup]
AppId={{8B84C3F0-7613-4B94-A3D9-9C9D7881B30E}
AppName=NotiGlow
AppVersion=5.0
VersionInfoVersion=5.0.0.0
VersionInfoTextVersion=5.0
AppPublisher=NotiGlow Project
AppPublisherURL=https://github.com/owergungor/NotiGlow
AppSupportURL=https://github.com/owergungor/NotiGlow/issues
AppUpdatesURL=https://github.com/owergungor/NotiGlow/releases
DefaultDirName={autopf}\NotiGlow
DefaultGroupName=NotiGlow
UninstallDisplayIcon={app}\NotiGlow.exe
OutputDir=release
OutputBaseFilename=NotiGlow-Setup-v5.0
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

#ifndef SourceDir
  #define SourceDir "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish"
#endif

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\NotiGlow"; Filename: "{app}\NotiGlow.exe"
Name: "{group}\{cm:UninstallProgram,NotiGlow}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NotiGlow"; Filename: "{app}\NotiGlow.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NotiGlow"; ValueData: """{app}\NotiGlow.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\NotiGlow.exe"; Description: "{cm:LaunchProgram,NotiGlow}"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopAppProcessesInDir(const TargetDir: string);
var
  ResultCode: Integer;
  PsCmd: string;
begin
  // Gracefully stop ONLY NotiGlow.exe and NotiGlow_Background.exe running from TargetDir
  // so other installations or versions are never touched, and %APPDATA%\NotiGlow is preserved.
  PsCmd := '-NoProfile -WindowStyle Hidden -Command "' +
    '$dir = ''' + TargetDir + '''; ' +
    'Get-CimInstance Win32_Process | Where-Object { ' +
    '  ($_.Name -eq ''NotiGlow.exe'' -or $_.Name -eq ''NotiGlow_Background.exe'') -and ' +
    '  $_.ExecutablePath -and $_.ExecutablePath.StartsWith($dir, [System.StringComparison]::OrdinalIgnoreCase) ' +
    '} | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"';
  Exec('powershell.exe', PsCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    StopAppProcessesInDir(ExpandConstant('{app}'));
  end;
end;

function InitializeUninstall(): Boolean;
begin
  StopAppProcessesInDir(ExpandConstant('{app}'));
  Result := True;
end;
