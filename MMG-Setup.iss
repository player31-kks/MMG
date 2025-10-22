[Setup]
AppId={{12345678-1234-1234-1234-123456789012}
AppName=MMG (Message & Message Generator)
AppVersion=1.0.0
AppVerName=MMG v1.0.0
AppPublisher=MMG Development Team
AppPublisherURL=https://github.com/player31-kks/MMG
AppSupportURL=https://github.com/player31-kks/MMG/issues
AppUpdatesURL=https://github.com/player31-kks/MMG/releases
DefaultDirName={autopf}\MMG
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
InfoBeforeFile=README.md
OutputDir=installer
OutputBaseFilename=MMG-Setup-v1.0.0
SetupIconFile=Resources\AppIcon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\MMG.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme
; 추가 필요한 파일들이 있다면 여기에 추가

[Icons]
Name: "{autoprograms}\MMG"; Filename: "{app}\MMG.exe"
Name: "{autodesktop}\MMG"; Filename: "{app}\MMG.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\MMG"; Filename: "{app}\MMG.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\MMG.exe"; Description: "{cm:LaunchProgram,MMG}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\MMG"

[Code]
procedure InitializeWizard;
begin
  WizardForm.LicenseAcceptedRadio.Checked := True;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox('이 프로그램은 64비트 Windows에서만 실행됩니다.', mbError, MB_OK);
    Result := False;
  end;
end;

[CustomMessages]
korean.LaunchProgram=MMG 실행하기
english.LaunchProgram=Launch MMG

[Registry]
Root: HKCU; Subkey: "Software\MMG"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey