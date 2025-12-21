#define AppName "JingleBox2"
#define AppVersion "1.0.0"
#define AppPublisher "Peter van de Pas"
#define AppExeName "JingleBox2.exe"
#define AppIcon "assets\app.ico"

[Setup]
; Use a real GUID. Create one once and keep it forever for upgrades.
; Inno syntax needs double {{ ... }}
AppId={{A1B2C3D4-E5F6-47A1-ABCD-123456789000}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; Installer EXE icon
SetupIconFile={#AppIcon}

; Wizard branding (ICO may work, BMP is recommended for best look)
WizardSmallImageFile={#AppIcon}
; WizardImageFile={#AppIcon}

OutputDir=output
OutputBaseFilename=JingleBox2-Setup
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\..\out\windows-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
