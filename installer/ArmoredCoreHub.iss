#ifndef AppVersion
  #error AppVersion must be supplied by scripts/package.ps1.
#endif
#ifndef PackageDir
  #error PackageDir must identify an audited, clean publish directory.
#endif
#ifndef ArtifactDir
  #error ArtifactDir must identify the release output directory.
#endif

[Setup]
AppId={{E78B46D7-67C0-4FD4-A678-C5DD12F936D3}
AppName=Armored Core Hub
AppVersion={#AppVersion}
AppPublisher=peachyGT
AppPublisherURL=https://github.com/peachyGT/armored-core-hub
AppSupportURL=https://github.com/peachyGT/armored-core-hub/issues
AppUpdatesURL=https://github.com/peachyGT/armored-core-hub/releases/latest
DefaultDirName={localappdata}\Programs\ArmoredCoreHub
DefaultGroupName=Armored Core Hub
DisableProgramGroupPage=yes
DisableWelcomePage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
SetupIconFile=..\src\Hub.App\Assets\Hub.ico
UninstallDisplayIcon={app}\ArmoredCoreHub.exe
AppMutex=Local\ArmoredCoreHub.SetupGuard
SetupMutex=Local\ArmoredCoreHub.Setup
CloseApplications=yes
CloseApplicationsFilter=ArmoredCoreHub.exe
RestartApplications=no
OutputDir={#ArtifactDir}
OutputBaseFilename=ArmoredCoreHub-Setup-win-x64
Compression=lzma2/normal
SolidCompression=yes
Uninstallable=yes

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Armored Core Hub"; Filename: "{app}\ArmoredCoreHub.exe"; WorkingDir: "{app}"; Check: not WizardNoIcons

[Run]
Filename: "{app}\ArmoredCoreHub.exe"; Description: "Open Armored Core Hub"; Flags: nowait postinstall skipifsilent

; No wildcard uninstall cleanup: personal data is never installer-owned.
