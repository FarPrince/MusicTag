; MusicTag installer (Inno Setup 6). Builds a self-contained, no-admin-required installer:
; Start Menu shortcut (always), an optional Desktop shortcut (Tasks checkbox), and registers/
; unregisters the Explorer "Open with MusicTag" context-menu entries via the app's own
; --register-explorer/--unregister-explorer CLI flags (see App.xaml.cs) rather than duplicating
; that HKCU registry-write logic here.
;
; Build: publish first, then compile this script:
;   dotnet publish ..\src\MusicTag.App -c Release -r win-x64 --self-contained true ^
;       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
;   iscc MusicTag.iss

#define MyAppName "MusicTag"
#define MyAppVersion "1.8"
#define MyAppPublisher "FarPrince"
#define MyAppURL "https://github.com/FarPrince/MP3Tag"
#define MyAppExeName "MusicTag.exe"
#define PublishDir "..\src\MusicTag.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
; Fixed, permanent identity for this app across versions/updates — do not regenerate.
AppId={{FAECFC12-FA2F-4843-BAE7-F2DBB2EF6BCA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user, no-admin install by default (matches the app's own HKCU-only Explorer-integration
; design — nothing this installer or the app does ever needs elevation), while still letting a
; user who wants an all-users install choose that via the elevation dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=output
OutputBaseFilename=MusicTag-Setup-{#MyAppVersion}
SetupIconFile=..\src\MusicTag.App\Assets\logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-explorer"; Flags: runhidden waituntilterminated; StatusMsg: "Registering Explorer context menu entries..."
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Runs before Inno removes the app's files, while MusicTag.exe still exists on disk —
; cleans up the HKCU context-menu entries the [Run] step above added.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-explorer"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterExplorer"
