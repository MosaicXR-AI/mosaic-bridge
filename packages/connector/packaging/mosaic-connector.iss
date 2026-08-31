; Inno Setup script for Mosaic Connector (Windows).
;
; Build on a Windows machine with Inno Setup 6 installed:
;   winget install JRSoftware.InnoSetup
;   iscc packaging\mosaic-connector.iss
;
; Produces build\Mosaic-Connector-Setup.exe: one program, no Node, no runtime.
; Unity projects are added from the app afterwards, because the bridge package
; belongs to a project rather than to the machine.

#define AppName "Mosaic Connector"
#define AppVersion "0.1.0"
#define AppPublisher "Mousa Soutari"
#define ExeName "mosaic-connector.exe"

[Setup]
AppId={{8E2C1F53-9E1B-4F0A-9C3D-6A1C7B2D4E90}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Mosaic Connector
DefaultGroupName=Mosaic
DisableProgramGroupPage=yes
OutputDir=..\build
OutputBaseFilename=Mosaic-Connector-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ChangesEnvironment=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\build\index-win-x64.exe"; DestDir: "{app}"; DestName: "{#ExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\Mosaic Connector (setup)"; Filename: "{app}\{#ExeName}"; Parameters: "setup"
Name: "{group}\Mosaic Connector (run)"; Filename: "{app}\{#ExeName}"; Parameters: "run"
Name: "{userdesktop}\Mosaic Connector"; Filename: "{app}\{#ExeName}"; Parameters: "run"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "addtopath"; Description: "Add to PATH so it can be run from any terminal"; GroupDescription: "Options:"

[Registry]
; PATH entry, so `mosaic-connector` works in PowerShell and cmd alike.
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
  ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; \
  Check: NeedsAddPath(ExpandConstant('{app}')); Tasks: addtopath

[Run]
Filename: "{app}\{#ExeName}"; Parameters: "setup"; Description: "Configure now (service address and access code)"; Flags: postinstall shellexec

[Messages]
WelcomeLabel2=This installs one small program that connects your Unity Editor to your Mosaic service.%n%nNothing from the Mosaic pipeline is installed on this machine. After installing, the setup step asks for your service address and access code, and offers to add the Mosaic Bridge package to the Unity projects it finds here.

[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;
