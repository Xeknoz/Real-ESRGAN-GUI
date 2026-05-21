#define AppName "Real-ESRGAN GUI"
#define AppPublisher "Xeknoz"
#define AppUrl "https://github.com/Xeknoz/Real-ESRGAN-GUI"

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef AppFileVersion
#define AppFileVersion "0.0.0.0"
#endif

#ifndef AppDisplayVersion
#define AppDisplayVersion AppVersion
#endif

#ifndef AppArchitecture
#define AppArchitecture "x64"
#endif

#ifndef AppSourceDir
#define AppSourceDir "..\..\artifacts\portable\x64"
#endif

[Setup]
AppId={{9DA5AC52-6320-4B81-8F4C-85FA1B7C1DBE}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppDisplayVersion} {#AppArchitecture}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\..\LICENSE
InfoBeforeFile=THIRD_PARTY_NOTICES.txt
OutputDir=..\..\artifacts\installers
OutputBaseFilename=Real-ESRGAN-GUI-Setup-{#AppArchitecture}
SetupIconFile=..\..\src\Launcher\app.ico
UninstallDisplayIcon={app}\Launcher.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousPrivileges=yes
#if AppArchitecture == "x86"
ArchitecturesAllowed=x86compatible and not x64compatible
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0
CloseApplications=yes
CloseApplicationsFilter=Launcher.exe,Real-ESRGAN GUI.exe
SetupLogging=yes
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} {#AppArchitecture} Installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppFileVersion}
VersionInfoVersion={#AppFileVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl,languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[CustomMessages]
english.AllUsersCopyDetected=Real-ESRGAN GUI is already installed for everyone on this computer. To update it, go back and choose "Install for all users". To install it only for you instead, uninstall the existing copy from Windows Settings first.
english.CurrentUserCopyDetected=Real-ESRGAN GUI is already installed only for you. To update it, go back and choose "Install just for me". To install it for everyone instead, uninstall the existing copy from Windows Settings first.
english.DowngradeDetected=A newer version of Real-ESRGAN GUI is already installed: %1. This installer contains version %2. Continue and replace it with the older version?
chinesesimplified.AllUsersCopyDetected=Real-ESRGAN GUI 已为这台电脑上的所有用户安装。若要更新，请返回并选择“为所有用户安装”；若要改为仅为你安装，请先在 Windows 设置中卸载现有版本。
chinesesimplified.CurrentUserCopyDetected=Real-ESRGAN GUI 已仅为你安装。若要更新，请返回并选择“仅为我安装”；若要改为给所有用户安装，请先在 Windows 设置中卸载现有版本。
chinesesimplified.DowngradeDetected=已安装较新的 Real-ESRGAN GUI：%1。当前安装包版本为 %2。是否继续并替换为旧版本？

[InstallDelete]
Type: filesandordirs; Name: "{app}\engine"
Type: filesandordirs; Name: "{app}\licenses"
Type: files; Name: "{app}\ARCHITECTURE.txt"
Type: files; Name: "{app}\CHANNEL.txt"
Type: files; Name: "{app}\LICENSE.txt"
Type: files; Name: "{app}\THIRD_PARTY_NOTICES.md"
Type: files; Name: "{app}\VERSION.txt"

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Launcher.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Launcher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Launcher.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  AppUninstallRegistryKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{9DA5AC52-6320-4B81-8F4C-85FA1B7C1DBE}_is1';
  SetupAppVersion = '{#AppVersion}';
  UnknownInstalledVersion = 'unknown';

function ReadVersionPart(var VersionText: String): Integer;
var
  DotPos: Integer;
  PartText: String;
begin
  DotPos := Pos('.', VersionText);
  if DotPos = 0 then begin
    PartText := VersionText;
    VersionText := '';
  end else begin
    PartText := Copy(VersionText, 1, DotPos - 1);
    Delete(VersionText, 1, DotPos);
  end;

  Result := StrToIntDef(PartText, 0);
end;

function CompareAppVersions(LeftVersion, RightVersion: String): Integer;
var
  Index: Integer;
  LeftPart: Integer;
  RightPart: Integer;
begin
  Result := 0;

  for Index := 1 to 4 do begin
    LeftPart := ReadVersionPart(LeftVersion);
    RightPart := ReadVersionPart(RightVersion);

    if LeftPart < RightPart then begin
      Result := -1;
      Exit;
    end;

    if LeftPart > RightPart then begin
      Result := 1;
      Exit;
    end;
  end;
end;

function TryReadInstalledVersion(RootKey: Integer; var InstalledVersion: String): Boolean;
begin
  Result := RegQueryStringValue(RootKey, AppUninstallRegistryKey, 'DisplayVersion', InstalledVersion);
  if not Result then begin
    Result := RegKeyExists(RootKey, AppUninstallRegistryKey);
    if Result then begin
      InstalledVersion := UnknownInstalledVersion;
    end;
  end;
end;

function TryReadAdminInstalledVersion(var InstalledVersion: String): Boolean;
begin
  Result :=
    TryReadInstalledVersion(HKLM, InstalledVersion) or
    TryReadInstalledVersion(HKLM32, InstalledVersion) or
    TryReadInstalledVersion(HKLM64, InstalledVersion);
end;

function TryReadSelectedModeInstalledVersion(var InstalledVersion: String): Boolean;
begin
  if IsAdminInstallMode then begin
    Result := TryReadAdminInstalledVersion(InstalledVersion);
  end else begin
    Result := TryReadInstalledVersion(HKCU, InstalledVersion);
  end;
end;

function TryReadOtherModeInstalledVersion(var InstalledVersion: String): Boolean;
begin
  if IsAdminInstallMode then begin
    Result := TryReadInstalledVersion(HKCU, InstalledVersion);
  end else begin
    Result := TryReadAdminInstalledVersion(InstalledVersion);
  end;
end;

function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  VersionComparison: Integer;
begin
  Result := True;

  if TryReadOtherModeInstalledVersion(InstalledVersion) then begin
    Log('Blocked setup because another install mode already exists. Installed version: ' + InstalledVersion);
    if not WizardSilent then begin
      if IsAdminInstallMode then begin
        MsgBox(CustomMessage('CurrentUserCopyDetected'), mbCriticalError, MB_OK);
      end else begin
        MsgBox(CustomMessage('AllUsersCopyDetected'), mbCriticalError, MB_OK);
      end;
    end;
    Result := False;
    Exit;
  end;

  if TryReadSelectedModeInstalledVersion(InstalledVersion) then begin
    if InstalledVersion <> UnknownInstalledVersion then begin
      VersionComparison := CompareAppVersions(InstalledVersion, SetupAppVersion);
      if VersionComparison > 0 then begin
        Log('Detected downgrade from ' + InstalledVersion + ' to ' + SetupAppVersion + '.');
        if WizardSilent then begin
          Result := False;
          Exit;
        end;

        Result := MsgBox(
          FmtMessage(CustomMessage('DowngradeDetected'), [InstalledVersion, SetupAppVersion]),
          mbConfirmation,
          MB_YESNO) = IDYES;
      end else if VersionComparison < 0 then begin
        Log('Detected in-place update from ' + InstalledVersion + ' to ' + SetupAppVersion + '.');
      end else begin
        Log('Detected same-version repair install for ' + SetupAppVersion + '.');
      end;
    end else begin
      Log('Detected existing install without DisplayVersion.');
    end;
  end;
end;
