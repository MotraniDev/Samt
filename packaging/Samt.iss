; SAMT / سَمت — personal Inno Setup installer
; Built by scripts/installer.ps1 (do not hardcode machine paths).
;
; Defines (passed by installer.ps1):
;   AppVersion  - e.g. 2026.7.13 or 0.7.0
;   SourceDir   - published app folder (contains Samt.App.exe)
;   OutputDir   - where Setup-*.exe is written
;   Arch        - x64 | x86 | arm64 (display / filename only)
;   AppIcon     - full path to AppIcon.ico

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\release\placeholder"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef AppIcon
  #define AppIcon "..\src\Samt.App\Assets\AppIcon.ico"
#endif

#define MyAppName "SAMT"
#define MyAppNameAr "سَمت"
#define MyAppPublisher "SAMT"
#define MyAppURL "https://github.com/MotraniDev/Samt"
#define MyAppExeName "Samt.App.exe"
#define MyAppId "{{A3F8C2E1-9B4D-4F6A-8E2C-1D0B7A5E9F34}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName} ({#MyAppNameAr})
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install (no admin) — matches personal tray app + LocalAppData settings
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=SAMT-Setup-{#AppVersion}-{#Arch}
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} ({#MyAppNameAr})
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
#if Arch == "x86"
ArchitecturesAllowed=x86compatible
#else
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
#endif
MinVersion=10.0.17763
; Four-part version for VersionInfo (Inno requirement)
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer - prayer times and adhan
VersionInfoProductName={#MyAppName}
LicenseFile=
InfoBeforeFile=
CloseApplications=yes
RestartApplications=no
; Allow reinstall / upgrade over previous personal install
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start SAMT with Windows (tray)"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--autostart"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Keep user settings by default; only remove empty install dir leftovers
Type: filesandordirs; Name: "{app}\*.pdb"
Type: dirifempty; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
