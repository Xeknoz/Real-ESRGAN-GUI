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
#define AppSourceDir "..\artifacts\portable\x64"
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
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installers
OutputBaseFilename=Real-ESRGAN-GUI-Setup-{#AppArchitecture}
SetupIconFile=..\src\Launcher\app.ico
UninstallDisplayIcon={app}\Launcher.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
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

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Launcher.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Launcher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Launcher.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
