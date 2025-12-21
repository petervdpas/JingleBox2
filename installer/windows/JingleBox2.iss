#define AppName "JingleBox2"
#define AppVersion "1.0.0"
#define AppPublisher "Peter van de Pas"
#define AppExeName "JingleBox2.exe"
#define SetupIco "assets\app.ico"

[Setup]
AppId={{A1B2C3D4-E5F6-47A1-ABCD-123456789000}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; Installer EXE icon (this one CAN be .ico)
SetupIconFile={#SetupIco}

OutputDir=output
OutputBaseFilename=JingleBox2-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes

[Files]
; App payload
Source: "..\..\out\windows-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

; Ship the icon file so shortcuts can explicitly use it (avoids Windows icon cache/lookup weirdness)
Source: "{#SetupIco}"; DestDir: "{app}"; DestName: "app.ico"; Flags: ignoreversion

[Icons]
; Explicit shortcut icons (this is the important part)
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app.ico"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\app.ico"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
