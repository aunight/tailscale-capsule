#ifndef AppVersion
  #define AppVersion "2.1.0"
#endif
#ifndef PublishRoot
  #define PublishRoot "..\artifacts\publish"
#endif
#ifndef ReleaseRoot
  #define ReleaseRoot "..\artifacts\release"
#endif
[Setup]
AppId=AuNight.TailscaleCapsule
AppName=Tailscale Capsule
AppVersion={#AppVersion}
AppPublisher=AuNight
AppPublisherURL=https://github.com/aunight/tailscale-capsule
AppSupportURL=https://github.com/aunight/tailscale-capsule/issues
AppUpdatesURL=https://github.com/aunight/tailscale-capsule/releases
DefaultDirName={localappdata}\Programs\TailscaleCapsule
DefaultGroupName=Tailscale Capsule
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#ReleaseRoot}
OutputBaseFilename=TailscaleCapsule-{#AppVersion}-Setup-x64
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\TailscaleCapsule.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=force
CloseApplicationsFilter=TailscaleCapsule.exe
RestartApplications=no
SetupLogging=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#PublishRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\Tailscale Capsule"; Filename: "{app}\TailscaleCapsule.exe"
Name: "{userdesktop}\Tailscale Capsule"; Filename: "{app}\TailscaleCapsule.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "TailscaleCapsule"; Flags: uninsdeletevalue
[Run]
Filename: "{app}\TailscaleCapsule.exe"; Description: "Launch Tailscale Capsule"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
var I: Integer;
begin
  I := 0;
  while WizardSilent and CheckForMutexes('Local\TailscaleCapsule.WinUI3') and (I < 100) do
  begin
    Sleep(100);
    I := I + 1;
  end;
  Result := True;
end;
