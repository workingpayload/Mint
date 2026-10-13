; Per-user Mint installer. Build with scripts/Build-Installer.ps1.
; Installs the self-contained app (bundled .NET and Windows App SDK) and the classic Explorer verb.
; The top-level Windows 11 menu needs the signed MSIX and is not provided here.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<published app folder>
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif
#if Arch == "x64"
  #define AllowedArchitectures "x64compatible"
#elif Arch == "ARM64"
  #define AllowedArchitectures "arm64"
#else
  #error Arch must be x64 or ARM64
#endif

#define AppExe "CreateFile.App.exe"
#define RepoUrl "https://github.com/workingpayload/Mint"

[Setup]
AppId={{639B58CD-9691-43B0-BE21-0963E342F454}
AppName=Mint
AppVersion={#AppVersion}
AppVerName=Mint {#AppVersion}
AppPublisher=Mint contributors
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#AppVersion}
PrivilegesRequired=lowest
DefaultDirName={autopf}\Mint
DefaultGroupName=Mint
DisableProgramGroupPage=yes
ArchitecturesAllowed={#AllowedArchitectures}
ArchitecturesInstallIn64BitMode={#AllowedArchitectures}
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Mint-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\src\CreateFile.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName=Mint
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes
CloseApplications=yes

[Tasks]
Name: "classicmenu"; Description: "Add ""Create New File"" to the Explorer right-click menu (Windows 11: under ""Show more options"")"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Mint"; Filename: "{app}\{#AppExe}"; Comment: "Create a new file on the Desktop"
Name: "{autoprograms}\Mint Settings"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"; Comment: "Mint presets, templates, and Explorer menu"

; Same keys and command format as ShellIntegration.EnableClassic. The \. suffix keeps a trailing separator from escaping the quote.
[Registry]
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CreateFile.New"; ValueType: string; ValueName: ""; ValueData: "Create New File"; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CreateFile.New"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExe}"",0"; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CreateFile.New\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" --folder ""%V\."""; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CreateFile.New"; ValueType: string; ValueName: ""; ValueData: "Create New File"; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CreateFile.New"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExe}"",0"; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CreateFile.New\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" --folder ""%1\."""; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\CreateFile.New"; ValueType: string; ValueName: ""; ValueData: "Create New File"; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\CreateFile.New"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExe}"",0"; Tasks: classicmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\CreateFile.New\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" --folder ""%V\."""; Tasks: classicmenu

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--settings"; Description: "Open Mint settings"; Flags: nowait postinstall skipifsilent

[Code]
// Removes only verbs that launch this installation, so an MSIX or script install elsewhere is left alone.
procedure RemoveVerb(Location: String);
var
  Key, Command: String;
begin
  Key := 'Software\Classes\' + Location + '\shell\CreateFile.New';
  if RegQueryStringValue(HKCU, Key + '\command', '', Command) and
     (Pos(Lowercase(ExpandConstant('{app}\{#AppExe}')), Lowercase(Command)) > 0) then
    RegDeleteKeyIncludingSubkeys(HKCU, Key);
end;

procedure RemoveClassicMenu();
begin
  RemoveVerb('Directory\Background');
  RemoveVerb('Directory');
  RemoveVerb('DesktopBackground');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('classicmenu') then
    RemoveClassicMenu();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveClassicMenu();
end;
