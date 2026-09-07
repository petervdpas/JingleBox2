#define AppName "JingleBox2"
; AppVersion is injected at build time via: iscc /DAppVersion=x.y.z
#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
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

; The payload is a 64-bit build, so run the installer in 64-bit mode.
; Without these, {autopf} lands in Program Files (x86) and the registry
; is written to the 32-bit view.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

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
; runasoriginaluser is the default here rather than a change: Inno documents it as the default
; behaviour when postinstall is used, so this launch was never the elevated one. It is said out
; loud all the same, for the reason every shipped machine names its own menu corner rather than
; leaning on the default: a flag nobody wrote down is one that moves the first time somebody
; edits the line, and what it decides is whether the launch that checks the install is the same
; launch as the shortcut.
;
; It is not the whole promise, and the gap is Windows' rather than Inno's. Setup started by
; right-clicking and choosing "Run as administrator", or from an already elevated process, never
; holds the original credentials to hand back, so this launch is elevated and the flag has no
; effect. Nothing in this file can reach that case.
;
; Which is why the app may not depend on it. It once did: the startup log was written to a path
; relative to the working directory, which is {app} here and therefore Program Files, so an
; elevated launch wrote it happily and a desktop shortcut threw before the toolkit was asked for
; anything. That log goes to the application folder now, so an install checked from the last
; page and an install checked from the shortcut are the same program either way.
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
{ Every release up to now ran the installer in 32-bit mode, so those copies sit in
  Program Files (x86) and their uninstall key is in the 32-bit registry view. This
  installer runs 64-bit and cannot see that key, so it would install alongside the old
  copy instead of over it. Find the old uninstaller ourselves and run it first.

  The GUID is the AppId above, written out again on purpose. It is a frozen historical
  value: what we are looking for is where the old versions registered themselves, which
  stays what it was even if AppId were ever to change. }
const
  LegacyKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A1B2C3D4-E5F6-47A1-ABCD-123456789000}_is1';

function LegacyUninstaller(): String;
var
  Value: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM32, LegacyKey, 'UninstallString', Value) then
    Result := RemoveQuotes(Value)
  else if RegQueryStringValue(HKCU32, LegacyKey, 'UninstallString', Value) then
    Result := RemoveQuotes(Value);
end;

procedure RemoveLegacyInstall();
var
  Uninstaller: String;
  ResultCode, Waited: Integer;
begin
  Uninstaller := LegacyUninstaller();
  if Uninstaller = '' then
  begin
    Log('No 32-bit-mode install registered; nothing to remove.');
    Exit;
  end;

  if not FileExists(Uninstaller) then
  begin
    { The key outlived the files. Leave it: the old uninstaller is what would clean it up. }
    Log('Stale uninstall key, no uninstaller at ' + Uninstaller);
    Exit;
  end;

  Log('Removing earlier install with ' + Uninstaller);
  if not Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '',
              SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Could not start the old uninstaller; installing anyway.');
    Exit;
  end;

  { An Inno uninstaller copies itself to a temporary folder and the process we waited on
    returns before that copy has finished its work. Wait for the original to go. }
  Waited := 0;
  while FileExists(Uninstaller) and (Waited < 60000) do
  begin
    Sleep(500);
    Waited := Waited + 500;
  end;
  Log('Earlier install removed, exit code ' + IntToStr(ResultCode));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  { Never block the install over this. A leftover copy is worse than untidy, but it is
    not worth refusing to install over. }
  RemoveLegacyInstall();
  Result := '';
end;
