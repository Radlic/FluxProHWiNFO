; Inno Setup 6 script for FluxProHWiNFO. Built by build\publish.ps1, which passes:
;   /DAppVersion=<x.y.z>   from Directory.Build.props
;   /DSourceDir=<folder>   the self-contained publish folder (exe, README, licence files)
;
; The first wizard page asks "Install for me only" or "Install for all users":
;   - Me only:   no admin rights. Installs to %LOCALAPPDATA%\Programs\FluxProHWiNFO, Start menu entry
;                for this user, Start with Windows via HKCU\...\Run (the same value the tray toggle uses).
;   - All users: needs admin (UAC). Installs to Program Files, Start menu entry for everyone, Start with
;                Windows via HKLM\...\Run, which starts the app for every user who signs in.
; Either way the app itself always runs as the signed-in user, never elevated, because HWiNFO's Gadget
; values live in that user's HKCU.

#ifndef AppVersion
  #error AppVersion is not defined. Build with build\publish.ps1.
#endif
#ifndef SourceDir
  #error SourceDir is not defined. Build with build\publish.ps1.
#endif

#define AppName "FluxProHWiNFO"
#define AppExe "FluxProHWiNFO.exe"
#define AppUrl "https://github.com/Radlic/FluxProHWiNFO"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
; Task Manager's Enabled/Disabled state for each Run entry. Windows 11 build 26300 skipped a Run entry
; that had no value here, so Setup writes the "enabled" value (02 + 11 zero bytes) along with the entry.
#define ApprovedKey "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"

[Setup]
; Never change AppId: Windows and Setup use it to find an existing install for upgrades and uninstall.
AppId={{08A11F75-4183-4439-9ACF-66A6423DFB36}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Radlic
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 Radlic
VersionInfoVersion={#AppVersion}
VersionInfoCompany=Radlic
VersionInfoDescription={#AppName} Setup
VersionInfoCopyright=Copyright (c) 2026 Radlic

; Me only / all users choice. "lowest" makes "me only" the default and needs no admin rights.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
; {autopf} is %LOCALAPPDATA%\Programs for "me only" and Program Files for "all users".
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Setup asks the user to exit the running tray app first (tray icon > Exit), then continues.
AppMutex=Local\FluxProHWiNFO.SingleInstance

WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
OutputBaseFilename={#AppName}-{#AppVersion}-win-x64-setup

[Tasks]
; Only one of these shows, depending on the install mode chosen on the first page.
Name: "autostart"; Description: "Start {#AppName} when I sign in to Windows"; Check: not IsAdminInstallMode
Name: "autostartall"; Description: "Start {#AppName} for everyone who signs in to this PC"; Check: IsAdminInstallMode

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
; {autoprograms}: this user's Start menu, or everyone's for an all-users install.
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Show CPU and GPU temperatures on the Antec Flux Pro display"

[Registry]
; HKA is HKCU for "me only" and HKLM for "all users". The value format matches what the app writes.
Root: HKA; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --background"; Tasks: autostart autostartall; Flags: uninsdeletevalue
Root: HKA; Subkey: "{#ApprovedKey}"; ValueType: binary; ValueName: "{#AppName}"; ValueData: "02 00 00 00 00 00 00 00 00 00 00 00"; Tasks: autostart autostartall; Flags: uninsdeletevalue

[Run]
; runasoriginaluser: after an all-users (elevated) install, start the app as the person who ran Setup,
; not as admin. An elevated copy could read a different HKCU and never see HWiNFO's values.
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function AutostartRoot(): Integer;
begin
  if IsAdminInstallMode then
    Result := HKEY_LOCAL_MACHINE
  else
    Result := HKEY_CURRENT_USER;
end;

{ True when a Run value starts an exe inside this install's folder. }
function PointsIntoApp(Value: String): Boolean;
begin
  Result := Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(Value)) > 0;
end;

procedure DeleteRunValueIfOurs(Root: Integer);
var
  Value: String;
begin
  if RegQueryStringValue(Root, '{#RunKey}', '{#AppName}', Value) and PointsIntoApp(Value) then
  begin
    RegDeleteValue(Root, '{#RunKey}', '{#AppName}');
    RegDeleteValue(Root, '{#ApprovedKey}', '{#AppName}');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { [Registry] only adds the value. If the Start with Windows box was unticked on a reinstall, remove it. }
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('autostart') and not WizardIsTaskSelected('autostartall') then
    DeleteRunValueIfOurs(AutostartRoot());
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    { The tray menu's Start with Windows toggle writes this user's HKCU value, which Setup didn't create. }
    DeleteRunValueIfOurs(HKEY_CURRENT_USER);
    if IsAdminInstallMode then
      DeleteRunValueIfOurs(HKEY_LOCAL_MACHINE);
  end;
end;
